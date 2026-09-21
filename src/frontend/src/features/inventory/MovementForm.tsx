import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { authenticatedRequest } from '../auth/session'
import { useAuth } from '../auth/AuthProvider'
import type { Product } from '../products/api'
import { inventoryError, move, quantityFormat } from './api'
import type { InventoryItem, MovementType, Receipt } from './api'

export function MovementForm({ item, type, onClose, onSaved }: {
  item: InventoryItem; type: MovementType; onClose: () => void; onSaved: (receipt: Receipt) => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [quantity, setQuantity] = useState('')
  const cache = useQueryClient()
  const { user } = useAuth()
  const current = useQuery({
    queryKey: ['inventory', user!.company.id, 'product', item.productId],
    queryFn: ({ signal }) => authenticatedRequest<Product>('/api/products/' + item.productId, { signal }),
    retry: false,
  })
  const mutation = useMutation({
    retry: false,
    mutationFn: (notes: string) => move(type, item.productId, Number(quantity), notes),
    onSuccess: async receipt => {
      await Promise.all([
        cache.invalidateQueries({ queryKey: ['inventory', user!.company.id] }),
        cache.invalidateQueries({ queryKey: ['products', user!.company.id] }),
      ])
      onSaved(receipt)
    },
    onError: () => { void current.refetch(); void cache.invalidateQueries({ queryKey: ['inventory', user!.company.id] }) },
  })
  useEffect(() => {
    const element = dialog.current!
    element.showModal()
    return () => element.close()
  }, [])
  const balance = current.data?.currentStock
  const insufficient = type === 'Exit' && balance !== undefined && Number(quantity) > balance
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutation.isPending || insufficient || !current.data?.isActive) return
    mutation.mutate(String(new FormData(event.currentTarget).get('notes')))
  }
  return <dialog className="inventory-dialog" ref={dialog} aria-labelledby="movement-title" onCancel={e => { e.preventDefault(); if (!mutation.isPending) onClose() }}>
    <header><div><p className="eyebrow">MOVIMENTAÇÃO</p><h2 id="movement-title">{type === 'Entry' ? 'Entrada de estoque' : 'Saída de estoque'}</h2></div>
      <button className="icon-button" title="Fechar" aria-label="Fechar movimentação" disabled={mutation.isPending} onClick={onClose}><X size={20} /></button></header>
    <div className="movement-context"><strong>{item.name}</strong><span>{item.sku}</span><p>Saldo atual: <strong>{balance === undefined ? '…' : quantityFormat.format(balance)}</strong></p></div>
    {current.isError && <div className="message error" role="alert">{inventoryError(current.error)} <button className="secondary" onClick={() => void current.refetch()}>Tentar novamente</button></div>}
    {current.data && !current.data.isActive && <p className="message warning" role="alert">Este produto está inativo.</p>}
    <form onSubmit={submit} aria-busy={mutation.isPending}>
      {mutation.isError && !insufficient && <p className="message error" role="alert">{inventoryError(mutation.error, true)}</p>}
      <fieldset disabled={mutation.isPending || !current.data?.isActive || current.isError}>
        <label>Quantidade<input autoFocus name="quantity" type="number" inputMode="decimal" min="0.001" max="99999999999.999" step="0.001" required value={quantity} onChange={e => setQuantity(e.target.value)} aria-describedby={insufficient ? 'quantity-error' : undefined} /></label>
        {insufficient && <p className="message error" id="quantity-error" role="alert">Estoque insuficiente para esta saída.</p>}
        <label>Observação <span className="optional">(opcional)</span><textarea name="notes" maxLength={1000} rows={3} /></label>
        <div className="movement-actions"><button className="secondary" type="button" disabled={mutation.isPending} onClick={onClose}>Cancelar</button><button className="primary" type="submit" disabled={insufficient}>{mutation.isPending ? 'Registrando…' : type === 'Entry' ? 'Registrar entrada' : 'Registrar saída'}</button></div>
      </fieldset>
    </form>
  </dialog>
}
