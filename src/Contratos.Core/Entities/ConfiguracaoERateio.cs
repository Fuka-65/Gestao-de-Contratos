using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Contratos.Core.Entities;

public class RateioDepartamento
{
    public int Id { get; set; }

    [Required]
    public int ContratoId { get; set; }

    [JsonIgnore]
    public Contrato? Contrato { get; set; }

    [Required(ErrorMessage = "O departamento é obrigatório.")]
    [StringLength(120)]
    public string Departamento { get; set; } = string.Empty;

    [Required(ErrorMessage = "O centro de custo é obrigatório.")]
    [StringLength(80)]
    public string CentroDeCusto { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    [Range(0.01, 100.00, ErrorMessage = "O percentual deve estar entre 0,01% e 100%.")]
    public decimal Percentual { get; set; }

    [StringLength(300)]
    public string? Observacao { get; set; }
}

public class ConfiguracaoSistema
{
    public int Id { get; set; } = 1;

    [Required]
    [StringLength(200)]
    public string RazaoSocialEmpresa { get; set; } = "Holding Corporativa Brasil S.A.";

    [Required]
    [StringLength(150)]
    public string NomeFantasiaEmpresa { get; set; } = "CorpGovernance CLM";

    [Required]
    [StringLength(20)]
    public string CnpjEmpresa { get; set; } = "12.345.678/0001-95";

    [StringLength(300)]
    public string? LogoEmpresaPath { get; set; }

    public bool SplashAtivo { get; set; } = true;

    [Range(1, 15)]
    public int SplashTempoSegundos { get; set; } = 2;

    [StringLength(150)]
    public string SplashTitulo { get; set; } = "Governança & Ciclo de Vida de Contratos";

    [StringLength(250)]
    public string SplashSlogan { get; set; } = "Excelência em Compliance Contratual, Prevenção de Riscos e Auditoria Financeira";

    [Range(15, 180)]
    public int MargemGlobalDiasAlertaVencimento { get; set; } = 60;

    // Integração Active Directory (LDAP)
    public bool LdapAtivo { get; set; } = false;

    [StringLength(150)]
    public string? LdapDominio { get; set; } = "corp.empresa.local";

    [StringLength(200)]
    public string? LdapServidor { get; set; } = "ldap://dc01.corp.empresa.local";

    public int LdapPorta { get; set; } = 389;

    [StringLength(250)]
    public string? LdapBaseDn { get; set; } = "DC=corp,DC=empresa,DC=local";

    [StringLength(150)]
    public string? LdapUsuarioLeitura { get; set; } = "svc_clm_ldap@corp.empresa.local";

    [StringLength(500)]
    public string? LdapSenhaLeituraEncrypted { get; set; }

    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
}

public class LogAuditoria
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
    public string UsuarioLogin { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public string Detalhes { get; set; } = string.Empty;
    public string? EnderecoIp { get; set; }
}
