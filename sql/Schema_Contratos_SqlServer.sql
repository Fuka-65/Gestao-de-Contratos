-- ================================================================================
-- SISTEMA DE GESTÃO DE CONTRATOS (CLM) & AUDITORIA FINANCEIRA
-- Script DDL Completo para Microsoft SQL Server (2019/2022+)
-- ================================================================================

SET NOCOUNT ON;
GO

-- 1. Tabela de Usuários Corporativos
IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        Id INT IDENTITY(1,1) NOT NULL,
        NomeCompleto NVARCHAR(150) NOT NULL,
        Email NVARCHAR(150) NOT NULL,
        Login NVARCHAR(100) NOT NULL,
        SenhaHash NVARCHAR(500) NOT NULL,
        Departamento NVARCHAR(100) NULL,
        Cargo NVARCHAR(100) NULL,
        PapeisCsv NVARCHAR(200) NOT NULL DEFAULT '3', -- Armazenamento dos papéis acumulados (1=Master, 2=Financeiro, 3=GestorContratos, 4=Auditor)
        Ativo BIT NOT NULL DEFAULT 1,
        AutenticacaoAd BIT NOT NULL DEFAULT 0,
        DataCriacao DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UltimoAcesso DATETIME2 NULL,
        CONSTRAINT PK_Usuarios PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Usuarios_Login UNIQUE (Login),
        CONSTRAINT UQ_Usuarios_Email UNIQUE (Email)
    );

    CREATE INDEX IX_Usuarios_Ativo ON dbo.Usuarios (Ativo);
END
GO

-- 2. Tabela de Fornecedores (Empresas Contratadas)
IF OBJECT_ID('dbo.Fornecedores', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Fornecedores (
        Id INT IDENTITY(1,1) NOT NULL,
        RazaoSocial NVARCHAR(200) NOT NULL,
        NomeFantasia NVARCHAR(150) NOT NULL,
        Cnpj NVARCHAR(20) NOT NULL,
        InscricaoEstadual NVARCHAR(50) NULL,
        InscricaoMunicipal NVARCHAR(50) NULL,
        ContatoComercialNome NVARCHAR(120) NULL,
        ContatoComercialTelefone NVARCHAR(40) NULL,
        ContatoComercialEmail NVARCHAR(150) NULL,
        ContatoFinanceiroNome NVARCHAR(120) NULL,
        ContatoFinanceiroTelefone NVARCHAR(40) NULL,
        ContatoFinanceiroEmail NVARCHAR(150) NULL,
        CentralSuporte NVARCHAR(100) NULL,
        SlaAtendimento NVARCHAR(100) NULL,
        Site NVARCHAR(250) NULL,
        LogoPath NVARCHAR(300) NULL,
        Ativo BIT NOT NULL DEFAULT 1,
        Observacoes NVARCHAR(1000) NULL,
        DataCadastro DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Fornecedores PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Fornecedores_Cnpj UNIQUE (Cnpj)
    );

    CREATE INDEX IX_Fornecedores_NomeFantasia ON dbo.Fornecedores (NomeFantasia);
    CREATE INDEX IX_Fornecedores_Ativo ON dbo.Fornecedores (Ativo);
END
GO

-- 3. Tabela de Contratos Corporativos
IF OBJECT_ID('dbo.Contratos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Contratos (
        Id INT IDENTITY(1,1) NOT NULL,
        NumeroContrato NVARCHAR(60) NOT NULL,
        TituloObjeto NVARCHAR(300) NOT NULL,
        FornecedorId INT NOT NULL,
        CategoriaContrato INT NOT NULL,
        DataAssinatura DATE NOT NULL,
        DataInicioVigencia DATE NOT NULL,
        DataTerminoVigencia DATE NOT NULL,
        ValorMensalContratado DECIMAL(18,2) NOT NULL,
        ValorGlobalContrato DECIMAL(18,2) NOT NULL,
        ValorOpexMensal DECIMAL(18,2) NULL,
        ValorCapexMensal DECIMAL(18,2) NULL,
        AliquotaImpostosPercentual DECIMAL(5,2) NOT NULL DEFAULT 14.25,
        Moeda NVARCHAR(10) NOT NULL DEFAULT 'BRL',
        IndiceReajuste INT NOT NULL DEFAULT 1,
        PeriodicidadeReajusteMeses INT NOT NULL DEFAULT 12,
        RenovaAutomaticamente BIT NOT NULL DEFAULT 0,
        DiasAvisoPrevioRescisao INT NOT NULL DEFAULT 30,
        DiasAlertaVencimento INT NOT NULL DEFAULT 60,
        CentroDeCusto NVARCHAR(80) NOT NULL,
        DepartamentoResponsavel NVARCHAR(120) NOT NULL,
        GestorContratoNome NVARCHAR(150) NOT NULL,
        GestorContratoEmail NVARCHAR(150) NOT NULL,
        Status INT NOT NULL DEFAULT 1,
        ArquivoContratoPath NVARCHAR(400) NULL,
        Observacoes NVARCHAR(1500) NULL,
        DataCriacao DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Contratos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Contratos_NumeroContrato UNIQUE (NumeroContrato),
        CONSTRAINT FK_Contratos_Fornecedores FOREIGN KEY (FornecedorId)
            REFERENCES dbo.Fornecedores (Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_Contratos_FornecedorId ON dbo.Contratos (FornecedorId);
    CREATE INDEX IX_Contratos_Status_DataTermino ON dbo.Contratos (Status, DataTerminoVigencia);
    CREATE INDEX IX_Contratos_Categoria ON dbo.Contratos (CategoriaContrato);
END
GO

-- 4. Tabela de Termos Aditivos
IF OBJECT_ID('dbo.TermosAditivos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TermosAditivos (
        Id INT IDENTITY(1,1) NOT NULL,
        ContratoId INT NOT NULL,
        NumeroAditivo NVARCHAR(60) NOT NULL,
        DataAssinatura DATE NOT NULL,
        TipoAditivo INT NOT NULL,
        NovaDataTermino DATE NULL,
        NovoValorMensal DECIMAL(18,2) NULL,
        DescricaoAlteracao NVARCHAR(1000) NOT NULL,
        ArquivoAditivoPath NVARCHAR(400) NULL,
        DataRegistro DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TermosAditivos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TermosAditivos_Contratos FOREIGN KEY (ContratoId)
            REFERENCES dbo.Contratos (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TermosAditivos_ContratoId ON dbo.TermosAditivos (ContratoId);
END
GO

-- 5. Tabela de Faturas, Medições e Auditoria Financeira
IF OBJECT_ID('dbo.FaturasGastos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FaturasGastos (
        Id INT IDENTITY(1,1) NOT NULL,
        ContratoId INT NOT NULL,
        FornecedorId INT NOT NULL,
        MesReferencia INT NOT NULL,
        AnoReferencia INT NOT NULL,
        ValorContratado DECIMAL(18,2) NOT NULL,
        ValorCobrado DECIMAL(18,2) NOT NULL,
        ValorPago DECIMAL(18,2) NULL,
        DataVencimento DATE NOT NULL,
        DataPagamento DATE NULL,
        NumeroNotaFiscal NVARCHAR(80) NOT NULL,
        CodigoBarrasLinhaDigitavel NVARCHAR(150) NULL,
        ArquivoFaturaPath NVARCHAR(400) NULL,
        Status INT NOT NULL DEFAULT 1,
        Observacoes NVARCHAR(1000) NULL,
        DataLancamento DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FaturasGastos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_FaturasGastos_Contratos FOREIGN KEY (ContratoId)
            REFERENCES dbo.Contratos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_FaturasGastos_Fornecedores FOREIGN KEY (FornecedorId)
            REFERENCES dbo.Fornecedores (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_FaturasGastos_Mes CHECK (MesReferencia BETWEEN 1 AND 12)
    );

    CREATE INDEX IX_FaturasGastos_ContratoId ON dbo.FaturasGastos (ContratoId);
    CREATE INDEX IX_FaturasGastos_FornecedorId ON dbo.FaturasGastos (FornecedorId);
    CREATE INDEX IX_FaturasGastos_Competencia ON dbo.FaturasGastos (AnoReferencia, MesReferencia);
    CREATE INDEX IX_FaturasGastos_Status ON dbo.FaturasGastos (Status);
END
GO

-- 6. Tabela de Rateio por Departamento e Centro de Custos
IF OBJECT_ID('dbo.RateiosDepartamentos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RateiosDepartamentos (
        Id INT IDENTITY(1,1) NOT NULL,
        ContratoId INT NOT NULL,
        Departamento NVARCHAR(120) NOT NULL,
        CentroDeCusto NVARCHAR(80) NOT NULL,
        Percentual DECIMAL(5,2) NOT NULL,
        Observacao NVARCHAR(300) NULL,
        CONSTRAINT PK_RateiosDepartamentos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RateiosDepartamentos_Contratos FOREIGN KEY (ContratoId)
            REFERENCES dbo.Contratos (Id) ON DELETE CASCADE,
        CONSTRAINT CK_RateiosDepartamentos_Percentual CHECK (Percentual > 0 AND Percentual <= 100)
    );

    CREATE INDEX IX_RateiosDepartamentos_ContratoId ON dbo.RateiosDepartamentos (ContratoId);
END
GO

-- 7. Tabela de Configurações Globais do Sistema e Integração LDAP
IF OBJECT_ID('dbo.ConfiguracoesSistema', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConfiguracoesSistema (
        Id INT IDENTITY(1,1) NOT NULL,
        RazaoSocialEmpresa NVARCHAR(200) NOT NULL,
        NomeFantasiaEmpresa NVARCHAR(150) NOT NULL,
        CnpjEmpresa NVARCHAR(20) NOT NULL,
        LogoEmpresaPath NVARCHAR(300) NULL,
        SplashAtivo BIT NOT NULL DEFAULT 1,
        SplashTempoSegundos INT NOT NULL DEFAULT 3,
        SplashTitulo NVARCHAR(150) NOT NULL DEFAULT 'Governança & Gestão de Contratos',
        SplashSlogan NVARCHAR(250) NOT NULL DEFAULT 'Excelência em Compliance Contratual e Auditoria Financeira',
        MargemGlobalDiasAlertaVencimento INT NOT NULL DEFAULT 60,
        AliquotaPadraoImpostosPercentual DECIMAL(5,2) NOT NULL DEFAULT 14.25,
        ModalidadeOrcamentoPadrao INT NOT NULL DEFAULT 1,
        TaxaInflacaoSimuladaPadrao DECIMAL(5,2) NOT NULL DEFAULT 4.50,
        LdapAtivo BIT NOT NULL DEFAULT 0,
        LdapDominio NVARCHAR(150) NULL,
        LdapServidor NVARCHAR(200) NULL,
        LdapPorta INT NOT NULL DEFAULT 389,
        LdapBaseDn NVARCHAR(250) NULL,
        LdapUsuarioLeitura NVARCHAR(150) NULL,
        LdapSenhaLeituraEncrypted NVARCHAR(500) NULL,
        UltimaAtualizacao DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ConfiguracoesSistema PRIMARY KEY CLUSTERED (Id)
    );
END
GO

-- 8. Tabela de Logs de Auditoria Corporativa
IF OBJECT_ID('dbo.LogsAuditoria', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LogsAuditoria (
        Id INT IDENTITY(1,1) NOT NULL,
        DataHora DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UsuarioLogin NVARCHAR(100) NOT NULL,
        Acao NVARCHAR(100) NOT NULL,
        Entidade NVARCHAR(100) NOT NULL,
        Detalhes NVARCHAR(2000) NOT NULL,
        EnderecoIp NVARCHAR(60) NULL,
        CONSTRAINT PK_LogsAuditoria PRIMARY KEY CLUSTERED (Id)
    );

    CREATE INDEX IX_LogsAuditoria_DataHora ON dbo.LogsAuditoria (DataHora DESC);
END
GO
