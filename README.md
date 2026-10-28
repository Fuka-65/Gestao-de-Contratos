# Indústrias Xhara — Sistema Corporativo de Gestão de Contratos (CLM), Orçamento & Auditoria Financeira

Solução corporativa desenvolvida em **.NET 8 LTS (C# 12)** com **Clean Architecture**, dedicada à Governança do Ciclo de Vida de Contratos Corporativos (**Contract Lifecycle Management - CLM**), Planejamento Orçamentário (**OPEX / CAPEX & Apuração de Impostos**) e Auditoria Financeira de Faturas das **Indústrias Xhara**.

---

## Principais Funcionalidades

- **Governança de Contratos & Fornecedores**:
  - Controle completo de vigências, marcos contratuais, índices de reajuste (`IPCA`, `IGP-M`, `INPC`, `Fixo`), termos aditivos e repositório digital em PDF.
  - **Exportação e Importação em Lote de Fornecedores (`.CSV`)**: Validação de dígitos verificadores de CNPJ (Módulo 11), bloqueio de duplicidades no mesmo arquivo, detecção automática de divergência de campos cadastrais com confirmação interativa (**Atualizar com CSV** vs. **Manter Registro Salvo**) e relatório de status da carga em tela.
- **Planejamento Orçamentário & Projeções Financeiras (`1 a N Meses`)**:
  - Projeção para horizontes **Mensal (1m)**, **Bimestral (2m)**, **Trimestral (3m)**, **Quadrimestral (4m)**, **Semestral (6m)**, **Anual (12m)** ou **Personalizado (`1 a 60 meses`)**.
  - Segregação contratual entre **OPEX** (Despesa Operacional) e **CAPEX** (Investimento em Capital).
  - Parametrização contábil por **Valor Bruto (Com Impostos)** ou **Valor Líquido (Sem Impostos)**, com contabilização em Dashboard dos **Impostos Apurados** no período e simulação de reajuste inflacionário nos aniversários contratuais.
- **Prevenção de Renovações Involuntárias**: Monitoramento ativo de prazos de aviso prévio para denúncia/rescisão antes da renovação automática.
- **Auditoria Financeira Automática**: Confronto em tempo real entre o valor faturado pelo fornecedor e o valor mensal homologado em contrato, sinalizando desvios monetários (`R$`) e percentuais (`%`).
- **Rateio por Departamento e Centro de Custos**: Alocação percentual de despesas contratuais entre múltiplos centros de custos corporativos.
- **Segurança Corporativa (RBAC + AES-256 + LDAP)**:
  - Segregação de papéis (`Master`, `Financeiro`, `GestorContratos`, `Auditor`) com **Regra de Acúmulo do Papel Mais Permissivo**.
  - **Imunidade Perpétua do `admin@admin`**: Protegido contra exclusão, desativação ou perda de privilégio Master, prevalecendo sempre sobre o Active Directory.
  - **Criptografia AES-256 + HMAC-SHA256**: Suporte a tokens `{ENC:...}` decriptados em tempo de execução e utilitário CLI `Contratos.CryptoTool`.
- **Identidade Visual Institucional Xhara**:
  - Logotipo e emblema oficiais das **Indústrias Xhara** (`xhara.com.br`) e aplicação rigorosa da paleta corporativa Pantone (`#004d5d`, `#007889`, `#f69f3a`, `#b6462e`, `#191919`, `#606060`, `#a2a7a7`, `#e8e7e5`, `#ffffff`).

---

## Estrutura da Solução (`Contratos.sln`)

```text
Contratos.sln
├── sql/
│   └── Schema_Contratos_SqlServer.sql      # Script DDL completo para SQL Server (Tabelas, FKs e Índices)
├── src/
│   ├── Contratos.Core/                     # Entidades, Enums, Interfaces, Serviços CSV, Validadores e ViewModels
│   ├── Contratos.Infrastructure/           # Repositórios (JSON Thread-Safe / EF Core SQL Server), AES-256 e LDAP
│   ├── Contratos.CryptoTool/               # CLI Console para encriptar segredos com token {ENC:...}
│   └── Contratos.Web/                      # ASP.NET Core MVC 8, Bootstrap 5.3, Bootstrap Icons 1.11.3, Chart.js
└── tests/
    └── Contratos.Tests/                    # Testes automatizados xUnit (RBAC, Criptografia, Concorrência, Orçamento e CSV)
```

---

## Como Compilar, Testar e Executar

### 1. Compilar a Solução
```bash
dotnet build Contratos.sln
```

### 2. Executar os Testes Automatizados (xUnit)
```bash
dotnet test Contratos.sln
```

### 3. Executar a Aplicação Web
```bash
dotnet run --project src/Contratos.Web/Contratos.Web.csproj
```
Acesse no navegador: `http://localhost:5150`

### Credenciais Padrão Inicializadas (Seed)
| Perfil | Usuário / Login | Senha |
| :--- | :--- | :--- |
| **Master (Imune)** | `admin@admin` | `Admin@Contratos2026!` |
| **Financeiro + Gestor** | `fernanda.ribeiro` | `Financeiro@2026!` |
| **Gestor de Contratos** | `carlos.mendes` | `Gestor@2026!` |
| **Auditor (Leitura)** | `marcelo.rocha` | `Auditor@2026!` |
