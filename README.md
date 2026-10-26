# Sistema Corporativo de Gestão de Contratos (CLM) & Auditoria Financeira

Solução corporativa desenvolvida em **.NET 8 LTS (C# 12)** com **Clean Architecture**, dedicada à Governança do Ciclo de Vida de Contratos Corporativos (**Contract Lifecycle Management - CLM**), Prevenção de Renovações Automáticas Involuntárias e Auditoria Financeira de Faturas.

---

## Principais Funcionalidades

- **Governança de Contratos & Fornecedores**: Controle completo de vigências, marcos contratuais, índices de reajuste (`IPCA`, `IGP-M`, `INPC`, `Fixo`), termos aditivos e repositório digital em PDF.
- **Prevenção de Renovações Involuntárias**: Monitoramento ativo de prazos de aviso prévio para denúncia/rescisão antes da renovação automática.
- **Auditoria Financeira Automática**: Confronto em tempo real entre o valor faturado pelo fornecedor e o valor mensal homologado em contrato, sinalizando desvios monetários (`R$`) e percentuais (`%`).
- **Rateio por Departamento e Centro de Custos**: Alocação percentual de despesas contratuais entre múltiplos centros de custos corporativos.
- **Segurança Corporativa (RBAC + AES-256 + LDAP)**:
  - Segregação de papéis (`Master`, `Financeiro`, `GestorContratos`, `Auditor`) com **Regra de Acúmulo do Papel Mais Permissivo**.
  - **Imunidade Perpétua do `admin@admin`**: Protegido contra exclusão, desativação ou perda de privilégio Master, prevalecendo sempre sobre o Active Directory.
  - **Criptografia AES-256 + HMAC-SHA256**: Suporte a tokens `{ENC:...}` decriptados em tempo de execução e utilitário CLI `Contratos.CryptoTool`.
- **Provedor de Banco de Dados Dual**:
  - **Modo Desenvolvimento (`Json`)**: Persistência atômica e *thread-safe* com `ReaderWriterLockSlim` e substituição segura via `.tmp`.
  - **Modo Produção (`SqlServer`)**: Microsoft Entity Framework Core para SQL Server com script DDL completo em `/sql/Schema_Contratos_SqlServer.sql`.

---

## Estrutura da Solução (`Contratos.sln`)

```text
Contratos.sln
├── sql/
│   └── Schema_Contratos_SqlServer.sql      # Script DDL completo para SQL Server (Tabelas, FKs e Índices)
├── src/
│   ├── Contratos.Core/                     # Entidades, Enums, Interfaces, Validadores e ViewModels
│   ├── Contratos.Infrastructure/           # Repositórios (JSON Thread-Safe / EF Core SQL Server), AES-256 e LDAP
│   ├── Contratos.CryptoTool/               # CLI Console para encriptar segredos com token {ENC:...}
│   └── Contratos.Web/                      # ASP.NET Core MVC 8, Bootstrap 5.3, Bootstrap Icons 1.11.3, Chart.js
└── tests/
    └── Contratos.Tests/                    # Testes automatizados xUnit (RBAC, Criptografia, Concorrência e Domínio)
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
