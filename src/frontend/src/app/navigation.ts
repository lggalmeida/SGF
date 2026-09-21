import { ChartNoAxesCombined, LayoutDashboard, Package, Boxes, Wallet, Settings } from 'lucide-react'

export const appNavigation = [
  { slug: 'dashboard', title: 'Visão geral', icon: LayoutDashboard, description: 'Um olhar sobre sua empresa.' },
  { slug: 'products', title: 'Produtos', icon: Package, description: 'Organize o catálogo de produtos da sua empresa.' },
  { slug: 'inventory', title: 'Estoque', icon: Boxes, description: 'Acompanhe entradas, saídas e a disponibilidade dos seus produtos.' },
  { slug: 'finance', title: 'Financeiro', icon: Wallet, description: 'Reúna as receitas e despesas da sua empresa.' },
  { slug: 'analytics', title: 'Analytics', icon: ChartNoAxesCombined, description: 'Transforme os dados da sua empresa em decisões.' },
  { slug: 'settings', title: 'Configurações', icon: Settings, description: 'Preferências e informações do seu espaço de trabalho.' },
] as const
