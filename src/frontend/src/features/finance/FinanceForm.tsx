import { useEffect, useRef } from 'react'
import type { FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { LoaderCircle, X } from 'lucide-react'
import { financeError, saveEntry } from './api'
import type { EntryType, FinancialEntry } from './api'

export function FinanceForm({ entry, type, onClose, onSaved }: {
  entry: FinancialEntry | null; type: EntryType; onClose: () => void; onSaved: () => Promise<void>
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const paid = entry?.status === 'Paid'
  const mutation = useMutation({ mutationFn: (data: Parameters<typeof saveEntry>[0]) => saveEntry(data, entry?.id), onSuccess: onSaved, retry: false })
  useEffect(() => { const el = dialog.current!; el.showModal(); return () => el.close() }, [])
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutation.isPending || paid) return
    const data = new FormData(event.currentTarget)
    mutation.mutate({
      type: entry?.type ?? String(data.get('type')) as EntryType,
      description: String(data.get('description')).trim(), category: String(data.get('category')).trim() || null,
      amount: Number(data.get('amount')), dueDate: String(data.get('dueDate')),
      notes: String(data.get('notes')).trim() || null,
    })
  }
  return <dialog ref={dialog} className="product-dialog finance-dialog" aria-labelledby="finance-form-title"
    onCancel={event => { event.preventDefault(); if (!mutation.isPending) onClose() }}>
    <div className="product-dialog-heading"><div><p className="eyebrow">FINANCEIRO</p>
      <h2 id="finance-form-title">{paid ? 'Detalhes do lançamento' : entry ? 'Editar lançamento' : type === 'Income' ? 'Nova receita' : 'Nova despesa'}</h2></div>
      <button className="icon-button" title="Fechar formulário" aria-label="Fechar formulário" disabled={mutation.isPending} onClick={onClose}><X size={20} /></button></div>
    {paid && <p className="message success">{entry.type === 'Income' ? 'Recebido' : 'Pago'} em {new Date(entry.paidAt!).toLocaleString('pt-BR')}</p>}
    <form onSubmit={submit} aria-busy={mutation.isPending}>
      {mutation.isError && <p role="alert" className="message error">{financeError(mutation.error, true)}</p>}
      <fieldset disabled={mutation.isPending || paid}>
        <label>Tipo<select name="type" defaultValue={entry?.type ?? type} disabled={Boolean(entry)}><option value="Income">Receita</option><option value="Expense">Despesa</option></select></label>
        <label>Descrição<input name="description" autoFocus required maxLength={200} pattern=".*\S.*" defaultValue={entry?.description} /></label>
        <label>Categoria <span className="optional">(opcional)</span><input name="category" maxLength={100} defaultValue={entry?.category ?? ''} /></label>
        <div className="product-price-fields"><label>Valor (R$)<input name="amount" type="number" inputMode="decimal" min="0.01" max="999999999999.99" step="0.01" required defaultValue={entry?.amount} /></label>
          <label>Vencimento<input name="dueDate" type="date" required min="0001-01-02" max="9999-12-31" defaultValue={entry?.dueDate} /></label></div>
        <label>Observação <span className="optional">(opcional)</span><textarea name="notes" rows={3} maxLength={2000} defaultValue={entry?.notes ?? ''} /></label>
      </fieldset>
      <div className="product-form-actions"><button className="secondary" type="button" disabled={mutation.isPending} onClick={onClose}>{paid ? 'Fechar' : 'Cancelar'}</button>
        {!paid && <button type="submit" className="primary" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle size={16} />}{mutation.isPending ? 'Salvando…' : 'Salvar lançamento'}</button>}</div>
    </form>
  </dialog>
}
