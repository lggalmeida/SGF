import { useEffect, useRef } from 'react'
import type { FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { X, LoaderCircle } from 'lucide-react'
import { productError, saveProduct } from './api'
import type { Product } from './api'

export function ProductForm({ product, onClose, onSaved }: {
  product: Product | null; onClose: () => void; onSaved: (editing: boolean) => Promise<void>
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const mutation = useMutation({
    mutationFn: (data: Parameters<typeof saveProduct>[0]) => saveProduct(data, product?.id),
    onSuccess: () => onSaved(Boolean(product)),
  })
  useEffect(() => {
    const element = dialog.current!
    element.showModal()
    return () => element.close()
  }, [])
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutation.isPending) return
    const data = new FormData(event.currentTarget)
    mutation.mutate({
      name: String(data.get('name')).trim(),
      sku: String(data.get('sku')).replace(/\s/g, '').toUpperCase(),
      description: String(data.get('description')).trim() || null,
      costPrice: Number(data.get('costPrice')), salePrice: Number(data.get('salePrice')),
      minimumStock: Number(data.get('minimumStock')),
    })
  }
  return <dialog className="product-dialog" ref={dialog} aria-labelledby="product-form-title"
    onCancel={event => { event.preventDefault(); if (!mutation.isPending) onClose() }}>
    <div className="product-dialog-heading"><div><p className="eyebrow">CATÁLOGO</p><h2 id="product-form-title">{product ? 'Editar produto' : 'Novo produto'}</h2></div>
      <button className="icon-button" aria-label="Fechar formulário" title="Fechar formulário" disabled={mutation.isPending} onClick={onClose}><X size={20} /></button></div>
    <form onSubmit={submit} aria-busy={mutation.isPending}>
      {mutation.isError && <p className="message error" role="alert">{productError(mutation.error)}</p>}
      <fieldset disabled={mutation.isPending}>
        <label>Nome<input autoFocus name="name" required maxLength={200} defaultValue={product?.name} /></label>
        <label>SKU<input name="sku" required maxLength={64} pattern=".*\S.*" defaultValue={product?.sku} autoCapitalize="characters" /></label>
        <label>Descrição <span className="optional">(opcional)</span><textarea name="description" maxLength={2000} rows={3} defaultValue={product?.description ?? ''} /></label>
        <div className="product-price-fields">
          <label>Preço de custo<input name="costPrice" type="number" inputMode="decimal" min="0" max="9999999999.99" step="0.01" required defaultValue={product?.costPrice ?? '0.00'} /></label>
          <label>Preço de venda<input name="salePrice" type="number" inputMode="decimal" min="0" max="9999999999.99" step="0.01" required defaultValue={product?.salePrice ?? '0.00'} /></label>
        </div>
        <label>Estoque mínimo<input name="minimumStock" type="number" inputMode="decimal" min="0" max="99999999999.999" step="0.001" required defaultValue={product?.minimumStock ?? 0} /></label>
        <div className="product-form-actions"><button type="button" className="secondary" onClick={onClose}>Cancelar</button>
          <button className="primary" type="submit">{mutation.isPending && <LoaderCircle size={16} aria-hidden="true" />}{mutation.isPending ? 'Salvando…' : 'Salvar produto'}</button></div>
      </fieldset>
    </form>
  </dialog>
}
