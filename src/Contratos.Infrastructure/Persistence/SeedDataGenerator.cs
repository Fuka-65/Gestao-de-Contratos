using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;

namespace Contratos.Infrastructure.Persistence;

public class ClmDatabaseSnapshot
{
    public List<Usuario> Usuarios { get; set; } = new();
    public List<Fornecedor> Fornecedores { get; set; } = new();
    public List<Contrato> Contratos { get; set; } = new();
    public List<TermoAditivo> TermosAditivos { get; set; } = new();
    public List<FaturaGasto> FaturasGastos { get; set; } = new();
    public List<RateioDepartamento> RateiosDepartamentos { get; set; } = new();
    public ConfiguracaoSistema Configuracao { get; set; } = new();
    public List<LogAuditoria> LogsAuditoria { get; set; } = new();
}

public static class SeedDataGenerator
{
    public const string DefaultAdminPassword = "Admin@Contratos2026!";

    public static ClmDatabaseSnapshot CreateInitialSeed(ICryptoService cryptoService)
    {
        var hoje = DateTime.Today;
        var snapshot = new ClmDatabaseSnapshot();

        // 1. Configuração Global do Sistema
        snapshot.Configuracao = new ConfiguracaoSistema
        {
            Id = 1,
            RazaoSocialEmpresa = "Vanguard Corporação Empresarial do Brasil S.A.",
            NomeFantasiaEmpresa = "Vanguard Governance CLM",
            CnpjEmpresa = "04.712.500/0001-07",
            SplashAtivo = true,
            SplashTempoSegundos = 2,
            SplashTitulo = "Governança & Ciclo de Vida de Contratos",
            SplashSlogan = "Controle Corporativo de Vigências, Prevenção de Riscos e Auditoria Financeira",
            MargemGlobalDiasAlertaVencimento = 60,
            LdapAtivo = false,
            LdapDominio = "corp.vanguard.com.br",
            LdapServidor = "ldap://dc01.corp.vanguard.com.br",
            LdapPorta = 389,
            LdapBaseDn = "DC=corp,DC=vanguard,DC=com,DC=br",
            LdapUsuarioLeitura = "svc_clm_reader@corp.vanguard.com.br",
            LdapSenhaLeituraEncrypted = cryptoService.EncryptWithToken("Ldap@ServiceSecret2026!"),
            UltimaAtualizacao = DateTime.UtcNow
        };

        // 2. Usuários Corporativos (incluindo admin@admin imune)
        snapshot.Usuarios = new List<Usuario>
        {
            new()
            {
                Id = 1,
                NomeCompleto = "Administrador Master Corporativo",
                Email = "admin@admin",
                Login = "admin@admin",
                SenhaHash = cryptoService.HashPassword(DefaultAdminPassword),
                Departamento = "Governança Corporativa & TI",
                Cargo = "Chief Information Security Officer (CISO)",
                PapeisCsv = ((int)UserRole.Master).ToString(),
                Ativo = true,
                AutenticacaoAd = false,
                DataCriacao = hoje.AddMonths(-12)
            },
            new()
            {
                Id = 2,
                NomeCompleto = "Fernanda Costa Ribeiro",
                Email = "fernanda.ribeiro@empresa.com.br",
                Login = "fernanda.ribeiro",
                SenhaHash = cryptoService.HashPassword("Financeiro@2026!"),
                Departamento = "Diretoria Financeira & Controladoria",
                Cargo = "Gerente de Controladoria e Auditoria",
                PapeisCsv = $"{(int)UserRole.Financeiro},{(int)UserRole.GestorContratos}",
                Ativo = true,
                AutenticacaoAd = false,
                DataCriacao = hoje.AddMonths(-8)
            },
            new()
            {
                Id = 3,
                NomeCompleto = "Carlos Eduardo Mendes",
                Email = "carlos.mendes@empresa.com.br",
                Login = "carlos.mendes",
                SenhaHash = cryptoService.HashPassword("Gestor@2026!"),
                Departamento = "Tecnologia da Informação",
                Cargo = "Coordenador de Contratos de TI",
                PapeisCsv = ((int)UserRole.GestorContratos).ToString(),
                Ativo = true,
                AutenticacaoAd = false,
                DataCriacao = hoje.AddMonths(-6)
            },
            new()
            {
                Id = 4,
                NomeCompleto = "Marcelo Tavares Rocha",
                Email = "marcelo.rocha@empresa.com.br",
                Login = "marcelo.rocha",
                SenhaHash = cryptoService.HashPassword("Auditor@2026!"),
                Departamento = "Compliance & Auditoria Interna",
                Cargo = "Auditor Sênior",
                PapeisCsv = ((int)UserRole.Auditor).ToString(),
                Ativo = true,
                AutenticacaoAd = false,
                DataCriacao = hoje.AddMonths(-4)
            }
        };

        // 3. Fornecedores Corporativos (CNPJs reais/válidos pelo módulo 11)
        snapshot.Fornecedores = new List<Fornecedor>
        {
            new()
            {
                Id = 1,
                RazaoSocial = "Microsoft Informática Ltda",
                NomeFantasia = "Microsoft Brasil (Enterprise Licensing)",
                Cnpj = "04.712.500/0001-07",
                InscricaoEstadual = "116.011.448.118",
                InscricaoMunicipal = "3.049.211-8",
                ContatoComercialNome = "Ricardo Vasconcelos (Enterprise Account Exec)",
                ContatoComercialTelefone = "(11) 5504-2100",
                ContatoComercialEmail = "ricardo.vasconcelos@microsoft-partner.com",
                ContatoFinanceiroNome = "Faturamento Enterprise Brasil",
                ContatoFinanceiroTelefone = "0800 888 4081",
                ContatoFinanceiroEmail = "billing.br@microsoft.com",
                CentralSuporte = "Premier Support 24x7 - Portal Azure/M365",
                SlaAtendimento = "Crítico (Sev A): 1 hora | Alta (Sev B): 4 horas",
                Site = "https://www.microsoft.com/pt-br",
                Ativo = true,
                Observacoes = "Contrato Enterprise Agreement (EA) para licenciamento Microsoft 365 E5, Dynamics e Power BI Pro.",
                DataCadastro = hoje.AddMonths(-14)
            },
            new()
            {
                Id = 2,
                RazaoSocial = "Amazon AWS Serviços Brasil Ltda",
                NomeFantasia = "Amazon Web Services (AWS Cloud)",
                Cnpj = "23.412.247/0001-10",
                InscricaoEstadual = "Isento",
                InscricaoMunicipal = "5.319.882-0",
                ContatoComercialNome = "Juliana Paiva (Technical Account Manager)",
                ContatoComercialTelefone = "(11) 4950-6600",
                ContatoComercialEmail = "jpaiva@amazon-aws-br.com",
                ContatoFinanceiroNome = "AWS Accounts Receivable LATAM",
                ContatoFinanceiroTelefone = "(11) 4950-6610",
                ContatoFinanceiroEmail = "aws-br-faturamento@amazon.com",
                CentralSuporte = "AWS Enterprise Support Console",
                SlaAtendimento = "99,99% Uptime SLA | Resposta < 15 min para produção crítica",
                Site = "https://aws.amazon.com/pt",
                Ativo = true,
                Observacoes = "Infraestrutura Cloud de Produção (EKS, RDS SQL Server, S3 e CloudFront) sob modelo Enterprise Discount Program (EDP).",
                DataCadastro = hoje.AddMonths(-12)
            },
            new()
            {
                Id = 3,
                RazaoSocial = "TOTVS S.A.",
                NomeFantasia = "TOTVS (ERP Protheus & RH)",
                Cnpj = "53.113.791/0001-22",
                InscricaoEstadual = "111.090.246.114",
                InscricaoMunicipal = "9.124.550-1",
                ContatoComercialNome = "Eduardo Fontes (Executivo de Contas)",
                ContatoComercialTelefone = "(11) 2099-7000",
                ContatoComercialEmail = "eduardo.fontes@totvs.com.br",
                ContatoFinanceiroNome = "Central Financeira TOTVS",
                ContatoFinanceiroTelefone = "0800 709 8100",
                ContatoFinanceiroEmail = "cobranca.corporativa@totvs.com.br",
                CentralSuporte = "Portal do Cliente / CST TOTVS",
                SlaAtendimento = "4 horas úteis para parada fiscal/faturamento",
                Site = "https://www.totvs.com",
                Ativo = true,
                Observacoes = "Sustentação, licenciamento CDUs e atualização fiscal do ERP corporativo e Folha de Pagamento.",
                DataCadastro = hoje.AddMonths(-18)
            },
            new()
            {
                Id = 4,
                RazaoSocial = "Grupo Protege Facilities & Vigilância Patrimonial S.A.",
                NomeFantasia = "Protege Facilities & Segurança",
                Cnpj = "43.283.811/0001-50",
                InscricaoEstadual = "108.452.990.111",
                InscricaoMunicipal = "2.871.109-4",
                ContatoComercialNome = "Marcos Vinícius Prado",
                ContatoComercialTelefone = "(11) 3882-9000",
                ContatoComercialEmail = "comercial.corporativo@protege.com.br",
                ContatoFinanceiroNome = "Ana Paula Medeiros (Medições & Faturamento)",
                ContatoFinanceiroTelefone = "(11) 3882-9045",
                ContatoFinanceiroEmail = "medicoes@protege.com.br",
                CentralSuporte = "CCO 24h: 0800 770 4511",
                SlaAtendimento = "Cobertura de posto de trabalho em até 2 horas",
                Site = "https://www.protege.com.br",
                Ativo = true,
                Observacoes = "Prestação de serviços contínuos de recepção, limpeza predial, manutenção civil e segurança patrimonial 24x7 na Matriz.",
                DataCadastro = hoje.AddMonths(-11)
            },
            new()
            {
                Id = 5,
                RazaoSocial = "Pinheiro Neto Consultoria Jurídica Empresarial",
                NomeFantasia = "Pinheiro & Associados Assessoria Jurídica",
                Cnpj = "60.613.477/0001-30",
                InscricaoEstadual = "Isento",
                InscricaoMunicipal = "1.440.912-7",
                ContatoComercialNome = "Dr. Henrique Cavalcanti (Sócio Diretor)",
                ContatoComercialTelefone = "(11) 3247-8400",
                ContatoComercialEmail = "hcavalcanti@pinheironeto-adv.com.br",
                ContatoFinanceiroNome = "Faturamento Jurídico",
                ContatoFinanceiroTelefone = "(11) 3247-8415",
                ContatoFinanceiroEmail = "financeiro@pinheironeto-adv.com.br",
                CentralSuporte = "Atendimento Direto Societário / Trabalhista",
                SlaAtendimento = "Pareceres urgentes em até 24 horas",
                Site = "https://www.pinheironeto.com.br",
                Ativo = true,
                Observacoes = "Retainer mensal para assessoria contratual, societária, LGPD e contencioso estratégico.",
                DataCadastro = hoje.AddMonths(-10)
            }
        };

        // 4. Contratos Corporativos (7 contratos com diferentes categorias e cenários de alerta)
        snapshot.Contratos = new List<Contrato>
        {
            // Contrato 1: Vencendo em 18 dias! (Dispara Alerta Warm Amber e Janela Crítica de Renovação Automática)
            new()
            {
                Id = 1,
                NumeroContrato = "CTR-2025-0014",
                TituloObjeto = "Licenciamento Corporativo Microsoft 365 E5 & Copilot Enterprise (450 Usuários)",
                FornecedorId = 1,
                CategoriaContrato = CategoriaContrato.LicenciamentoSoftware,
                DataAssinatura = hoje.AddMonths(-11).AddDays(-12),
                DataInicioVigencia = hoje.AddMonths(-11).AddDays(-12),
                DataTerminoVigencia = hoje.AddDays(18), // Menos de 30 dias!
                ValorMensalContratado = 48500.00m,
                ValorGlobalContrato = 582000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.IPCA,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = true,
                DiasAvisoPrevioRescisao = 30,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-1020-TI",
                DepartamentoResponsavel = "Tecnologia da Informação",
                GestorContratoNome = "Carlos Eduardo Mendes",
                GestorContratoEmail = "carlos.mendes@empresa.com.br",
                Status = StatusContrato.Vigente,
                ArquivoContratoPath = "/uploads/contratos/CTR-2025-0014-Microsoft-EA.pdf",
                Observacoes = "ATENÇÃO: Exige revisão do quantitativo de licenças antes da renovação automática anual."
            },
            // Contrato 2: AWS Cloud (Possui fatura com desvio no mês atual para disparar Terracotta Red)
            new()
            {
                Id = 2,
                NumeroContrato = "CTR-2025-0029",
                TituloObjeto = "Infraestrutura em Nuvem AWS Enterprise (Produção, DR & Banco de Dados)",
                FornecedorId = 2,
                CategoriaContrato = CategoriaContrato.ServicosTI,
                DataAssinatura = hoje.AddMonths(-8),
                DataInicioVigencia = hoje.AddMonths(-8),
                DataTerminoVigencia = hoje.AddMonths(16),
                ValorMensalContratado = 62000.00m,
                ValorGlobalContrato = 1488000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.IPCA,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = true,
                DiasAvisoPrevioRescisao = 60,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-1025-CLOUD",
                DepartamentoResponsavel = "Engenharia de Plataforma & Cloud",
                GestorContratoNome = "Carlos Eduardo Mendes",
                GestorContratoEmail = "carlos.mendes@empresa.com.br",
                Status = StatusContrato.Vigente,
                ArquivoContratoPath = "/uploads/contratos/CTR-2025-0029-AWS-EDP.pdf",
                Observacoes = "Baseline mensal contratado de R$ 62.000,00. Qualquer excedente acima do teto requer aprovação prévia de capacidade."
            },
            // Contrato 3: TOTVS ERP (Vigente tranquilo)
            new()
            {
                Id = 3,
                NumeroContrato = "CTR-2024-0088",
                TituloObjeto = "Sustentação, Atualização Legal e Licenciamento ERP TOTVS Protheus",
                FornecedorId = 3,
                CategoriaContrato = CategoriaContrato.LicenciamentoSoftware,
                DataAssinatura = hoje.AddMonths(-18),
                DataInicioVigencia = hoje.AddMonths(-6),
                DataTerminoVigencia = hoje.AddMonths(6),
                ValorMensalContratado = 34200.00m,
                ValorGlobalContrato = 410400.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.IGPM,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = true,
                DiasAvisoPrevioRescisao = 60,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-2010-SISTEMAS",
                DepartamentoResponsavel = "Sistemas Corporativos & Backoffice",
                GestorContratoNome = "Fernanda Costa Ribeiro",
                GestorContratoEmail = "fernanda.ribeiro@empresa.com.br",
                Status = StatusContrato.Vigente,
                ArquivoContratoPath = "/uploads/contratos/CTR-2024-0088-TOTVS.pdf",
                Observacoes = "Renovado via 1º Termo Aditivo com aplicação de reajuste negociado."
            },
            // Contrato 4: Protege Facilities (Vencendo em 25 dias + Possui desvio de fatura no mês atual!)
            new()
            {
                Id = 4,
                NumeroContrato = "CTR-2025-0041",
                TituloObjeto = "Gestão Integrada de Facilities, Limpeza, Recepção e Segurança Patrimonial 24x7",
                FornecedorId = 4,
                CategoriaContrato = CategoriaContrato.Facilities,
                DataAssinatura = hoje.AddMonths(-11).AddDays(-5),
                DataInicioVigencia = hoje.AddMonths(-11).AddDays(-5),
                DataTerminoVigencia = hoje.AddDays(25), // Menos de 30 dias!
                ValorMensalContratado = 78000.00m,
                ValorGlobalContrato = 936000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.INPC,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = true,
                DiasAvisoPrevioRescisao = 45,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-3010-ADM",
                DepartamentoResponsavel = "Operações & Patrimônio",
                GestorContratoNome = "Patrícia Lima Oliveira",
                GestorContratoEmail = "patricia.oliveira@empresa.com.br",
                Status = StatusContrato.Vigente,
                ArquivoContratoPath = "/uploads/contratos/CTR-2025-0041-Protege-Facilities.pdf",
                Observacoes = "Contrato com cláusula de renovação automática caso não denunciado 45 dias antes do término."
            },
            // Contrato 5: Assessoria Jurídica & Compliance LGPD
            new()
            {
                Id = 5,
                NumeroContrato = "CTR-2025-0055",
                TituloObjeto = "Assessoria Jurídica Empresarial Contínua, Contratos e Governança LGPD",
                FornecedorId = 5,
                CategoriaContrato = CategoriaContrato.AssessoriaJuridica,
                DataAssinatura = hoje.AddMonths(-7),
                DataInicioVigencia = hoje.AddMonths(-7),
                DataTerminoVigencia = hoje.AddMonths(5),
                ValorMensalContratado = 22500.00m,
                ValorGlobalContrato = 270000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.IPCA,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = false,
                DiasAvisoPrevioRescisao = 30,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-4010-JUR",
                DepartamentoResponsavel = "Jurídico & Governança",
                GestorContratoNome = "Roberto Almeida Santos",
                GestorContratoEmail = "roberto.santos@empresa.com.br",
                Status = StatusContrato.Vigente,
                ArquivoContratoPath = "/uploads/contratos/CTR-2025-0055-Juridico.pdf",
                Observacoes = "Franquia mensal de 90 horas técnicas sênior inclusas no escopo."
            },
            // Contrato 6: Locação da Sede Corporativa (Vencendo em 48 dias - dentro da janela de 60 dias)
            new()
            {
                Id = 6,
                NumeroContrato = "CTR-2022-0003",
                TituloObjeto = "Locação Comercial dos Conjuntos 141 a 144 - Edifício Corporate Tower SP",
                FornecedorId = 4,
                CategoriaContrato = CategoriaContrato.LocacaoImovel,
                DataAssinatura = hoje.AddMonths(-34),
                DataInicioVigencia = hoje.AddMonths(-34),
                DataTerminoVigencia = hoje.AddDays(48),
                ValorMensalContratado = 54000.00m,
                ValorGlobalContrato = 1944000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.IGPM,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = true,
                DiasAvisoPrevioRescisao = 90,
                DiasAlertaVencimento = 60,
                CentroDeCusto = "CC-3005-SEDE",
                DepartamentoResponsavel = "Diretoria Administrativa",
                GestorContratoNome = "Fernanda Costa Ribeiro",
                GestorContratoEmail = "fernanda.ribeiro@empresa.com.br",
                Status = StatusContrato.EmRenovacao,
                ArquivoContratoPath = "/uploads/contratos/CTR-2022-0003-Locacao-Sede.pdf",
                Observacoes = "Em negociação de aditivo para substituição do índice IGP-M pelo IPCA."
            },
            // Contrato 7: Consultoria Estratégica em Segurança (Vencido há 5 dias para alerta vermelho de vigência)
            new()
            {
                Id = 7,
                NumeroContrato = "CTR-2025-0092",
                TituloObjeto = "Consultoria Especializada em Pentest Contínuo e Adequação ISO 27001",
                FornecedorId = 1,
                CategoriaContrato = CategoriaContrato.Consultoria,
                DataAssinatura = hoje.AddMonths(-6).AddDays(-5),
                DataInicioVigencia = hoje.AddMonths(-6).AddDays(-5),
                DataTerminoVigencia = hoje.AddDays(-5), // Vencido há 5 dias!
                ValorMensalContratado = 18000.00m,
                ValorGlobalContrato = 108000.00m,
                Moeda = "BRL",
                IndiceReajuste = IndiceReajuste.FixoSemReajuste,
                PeriodicidadeReajusteMeses = 12,
                RenovaAutomaticamente = false,
                DiasAvisoPrevioRescisao = 15,
                DiasAlertaVencimento = 30,
                CentroDeCusto = "CC-1090-SEC",
                DepartamentoResponsavel = "Segurança da Informação",
                GestorContratoNome = "Carlos Eduardo Mendes",
                GestorContratoEmail = "carlos.mendes@empresa.com.br",
                Status = StatusContrato.Vigente, // Permanece vigente no cadastro mas DataTerminoVigencia < hoje => EstaVencido() == true
                ArquivoContratoPath = "/uploads/contratos/CTR-2025-0092-ISO27001.pdf",
                Observacoes = "Vigência expirada há 5 dias. Necessário formalizar termo de encerramento ou aditivo de prorrogação."
            }
        };

        // 5. Termos Aditivos
        snapshot.TermosAditivos = new List<TermoAditivo>
        {
            new()
            {
                Id = 1,
                ContratoId = 3,
                NumeroAditivo = "1º TA - CTR-2024-0088",
                DataAssinatura = hoje.AddMonths(-6),
                TipoAditivo = TipoAditivo.PrazoEValor,
                NovaDataTermino = hoje.AddMonths(6),
                NovoValorMensal = 34200.00m,
                DescricaoAlteracao = "Prorrogação de vigência por 12 meses adicionais com reajuste negociado de 4,5% (abaixo do IGP-M acumulado).",
                ArquivoAditivoPath = "/uploads/aditivos/TA01-CTR-2024-0088.pdf",
                DataRegistro = hoje.AddMonths(-6)
            },
            new()
            {
                Id = 2,
                ContratoId = 1,
                NumeroAditivo = "1º TA - CTR-2025-0014",
                DataAssinatura = hoje.AddMonths(-4),
                TipoAditivo = TipoAditivo.AlteracaoValor,
                NovaDataTermino = null,
                NovoValorMensal = 48500.00m,
                DescricaoAlteracao = "Acréscimo de 30 licenças Microsoft Copilot for M365 para diretoria executiva e jurídico.",
                ArquivoAditivoPath = "/uploads/aditivos/TA01-CTR-2025-0014.pdf",
                DataRegistro = hoje.AddMonths(-4)
            }
        };

        // 6. Rateio por Departamento e Centro de Custos
        snapshot.RateiosDepartamentos = new List<RateioDepartamento>
        {
            new() { Id = 1, ContratoId = 1, Departamento = "Tecnologia da Informação", CentroDeCusto = "CC-1020-TI", Percentual = 45.00m, Observacao = "Licenças técnicas e administração tenant" },
            new() { Id = 2, ContratoId = 1, Departamento = "Diretoria Comercial & Vendas", CentroDeCusto = "CC-5010-COM", Percentual = 35.00m, Observacao = "Equipes comerciais e CRM" },
            new() { Id = 3, ContratoId = 1, Departamento = "Finanças & Backoffice", CentroDeCusto = "CC-2010-FIN", Percentual = 20.00m, Observacao = "Usuários administrativos e Power BI" },

            new() { Id = 4, ContratoId = 2, Departamento = "Engenharia de Plataforma & Cloud", CentroDeCusto = "CC-1025-CLOUD", Percentual = 70.00m, Observacao = "Ambientes produtivos e Kubernetes" },
            new() { Id = 5, ContratoId = 2, Departamento = "Dados & Analytics", CentroDeCusto = "CC-1040-DATA", Percentual = 30.00m, Observacao = "Data Lake e processamento analítico" },

            new() { Id = 6, ContratoId = 3, Departamento = "Diretoria Financeira & Fiscal", CentroDeCusto = "CC-2010-FIN", Percentual = 60.00m, Observacao = "Módulos Contábil, Fiscal e Financeiro" },
            new() { Id = 7, ContratoId = 3, Departamento = "Recursos Humanos", CentroDeCusto = "CC-3050-RH", Percentual = 40.00m, Observacao = "Folha de pagamento e ponto eletrônico" },

            new() { Id = 8, ContratoId = 4, Departamento = "Operações & Patrimônio", CentroDeCusto = "CC-3010-ADM", Percentual = 100.00m, Observacao = "Sede Administrativa Matriz" },
            new() { Id = 9, ContratoId = 5, Departamento = "Jurídico & Governança", CentroDeCusto = "CC-4010-JUR", Percentual = 100.00m, Observacao = "Assessoria corporativa centralizada" },
            new() { Id = 10, ContratoId = 6, Departamento = "Diretoria Administrativa", CentroDeCusto = "CC-3005-SEDE", Percentual = 100.00m, Observacao = "Ocupação predial sede" }
        };

        // 7. Histórico de Faturas dos últimos 6 meses (incluindo desvios no mês atual para Auditoria Financeira)
        var faturas = new List<FaturaGasto>();
        int faturaId = 1;

        for (int offsetMes = 5; offsetMes >= 0; offsetMes--)
        {
            var dataRef = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-offsetMes);
            int mes = dataRef.Month;
            int ano = dataRef.Year;
            bool ehMesAtual = offsetMes == 0;

            foreach (var contrato in snapshot.Contratos.Where(c => c.Id <= 6))
            {
                decimal valorContratado = contrato.ValorMensalContratado;
                decimal valorCobrado = valorContratado;
                StatusFatura status = ehMesAtual ? StatusFatura.AprovadaParaPagamento : StatusFatura.Paga;
                string? observacao = "Fatura conferida automaticamente em conformidade com o valor homologado em contrato.";

                // Cenário 1 de Desvio no mês atual: AWS cobrando R$ 71.450,00 contra R$ 62.000,00 contratados (+R$ 9.450,00 / +15,24%)
                if (ehMesAtual && contrato.Id == 2)
                {
                    valorCobrado = 71450.00m;
                    status = StatusFatura.EmDisputaContestada;
                    observacao = "ALERTA DE AUDITORIA: Cobrança excedente de R$ 9.450,00 (+15,24%) referente a instâncias EC2 sob demanda não homologadas em aditivo.";
                }
                // Cenário 2 de Desvio no mês atual: Protege Facilities cobrando R$ 84.900,00 contra R$ 78.000,00 contratados (+R$ 6.900,00 / +8,85%)
                else if (ehMesAtual && contrato.Id == 4)
                {
                    valorCobrado = 84900.00m;
                    status = StatusFatura.EmDisputaContestada;
                    observacao = "ALERTA DE AUDITORIA: Cobrança indevida de R$ 6.900,00 (+8,85%) por taxa de horas extras e reajuste aplicado sem Termo Aditivo assinado.";
                }
                // Cenário histórico há 2 meses na AWS já auditado/glosado
                else if (offsetMes == 2 && contrato.Id == 2)
                {
                    valorCobrado = 64800.00m;
                    status = StatusFatura.Paga;
                    observacao = "Pequeno excedente de tráfego de rede (+R$ 2.800,00) justificado pela migração trimestral.";
                }

                var vencimento = new DateTime(ano, mes, Math.Min(15, DateTime.DaysInMonth(ano, mes)));
                faturas.Add(new FaturaGasto
                {
                    Id = faturaId++,
                    ContratoId = contrato.Id,
                    FornecedorId = contrato.FornecedorId,
                    MesReferencia = mes,
                    AnoReferencia = ano,
                    ValorContratado = valorContratado,
                    ValorCobrado = valorCobrado,
                    ValorPago = status == StatusFatura.Paga ? valorCobrado : null,
                    DataVencimento = vencimento,
                    DataPagamento = status == StatusFatura.Paga ? vencimento.AddDays(-1) : null,
                    NumeroNotaFiscal = $"NFS-{ano}{mes:D2}-{contrato.Id:D3}{faturaId:D2}",
                    CodigoBarrasLinhaDigitavel = $"34191.79001 01043.510047 91020.150008 { (faturaId % 9) + 1 } 98760000{ (int)valorCobrado }",
                    ArquivoFaturaPath = $"/uploads/faturas/NFS-{ano}{mes:D2}-{contrato.Id:D3}.pdf",
                    Status = status,
                    Observacoes = observacao,
                    DataLancamento = vencimento.AddDays(-10)
                });
            }
        }

        snapshot.FaturasGastos = faturas;

        // 8. Logs de Auditoria Iniciais
        snapshot.LogsAuditoria = new List<LogAuditoria>
        {
            new()
            {
                Id = 1,
                DataHora = DateTime.UtcNow.AddHours(-4),
                UsuarioLogin = "admin@admin",
                Acao = "INICIALIZACAO_SISTEMA",
                Entidade = "Sistema",
                Detalhes = "Base corporativa CLM inicializada com políticas RBAC e criptografia AES-256.",
                EnderecoIp = "127.0.0.1"
            },
            new()
            {
                Id = 2,
                DataHora = DateTime.UtcNow.AddHours(-2),
                UsuarioLogin = "fernanda.ribeiro",
                Acao = "AUDITORIA_FATURA_DESVIO",
                Entidade = "FaturaGasto",
                Detalhes = "Fatura NFS do contrato CTR-2025-0029 (AWS) sinalizada em disputa por desvio de +15,24% acima do valor contratado.",
                EnderecoIp = "10.20.1.45"
            }
        };

        return snapshot;
    }
}
