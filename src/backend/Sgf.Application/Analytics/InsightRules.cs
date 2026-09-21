using System.Globalization;

namespace Sgf.Application.Analytics;

public static class InsightRules
{
    public static AnalysisInsight[] Evaluate(FinancialIndicators finance, FinancialComparisons comparison,
        InventoryIndicators inventory, StockOutProduct[] top)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var result = new List<AnalysisInsight>();
        if (inventory.ZeroStockProducts > 0)
            result.Add(new("zero_stock", "attention", $"{inventory.ZeroStockProducts} produto(s) sem estoque.",
                "Produtos ativos com saldo atual igual a zero."));
        if (inventory.LowStockProducts > 0)
            result.Add(new("low_stock", "attention", $"{inventory.LowStockProducts} produto(s) no limite ou abaixo do estoque mínimo.",
                "Produtos ativos com saldo menor ou igual ao mínimo, incluindo os zerados."));
        if (finance.OverduePayable > 0)
            result.Add(new("overdue_payable", "attention", $"{finance.OverduePayable.ToString("C", culture)} em contas a pagar vencidas.",
                "Despesas pendentes com vencimento anterior a hoje."));
        if (finance.OverdueReceivable > 0)
            result.Add(new("overdue_receivable", "attention", $"{finance.OverdueReceivable.ToString("C", culture)} em contas a receber vencidas.",
                "Receitas pendentes com vencimento anterior a hoje."));
        if (comparison.Paid.ChangePercent >= 10)
            result.Add(new("expense_increase", "attention", $"Despesas pagas aumentaram {comparison.Paid.ChangePercent.Value.ToString("0.#", culture)}%.",
                "Comparação entre os intervalos informados; aumento de pelo menos 10% sobre base anterior positiva."));
        if (comparison.Received.ChangePercent <= -10)
            result.Add(new("income_decrease", "attention", $"Receitas recebidas caíram {Math.Abs(comparison.Received.ChangePercent.Value).ToString("0.#", culture)}%.",
                "Comparação entre os intervalos informados; queda de pelo menos 10% sobre base anterior positiva."));
        if (inventory.StaleProducts > 0)
            result.Add(new("stale_products", "info", $"{inventory.StaleProducts} produto(s) sem movimentação nos últimos 30 dias.",
                "Produtos ativos cadastrados há pelo menos 30 dias, sem entradas ou saídas nessa janela."));
        if (top.Length > 0)
            result.Add(new("top_stock_out", "info", $"{top[0].Name} lidera as saídas: {top[0].Quantity.ToString("N3", culture)}.",
                "Maior quantidade registrada em saídas no período. Movimentação física, não necessariamente venda; empates usam nome e ID."));
        return result.ToArray();
    }
}
