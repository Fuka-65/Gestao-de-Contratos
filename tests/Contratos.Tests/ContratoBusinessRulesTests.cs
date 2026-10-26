using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Validators;
using Xunit;

namespace Contratos.Tests;

public class ContratoBusinessRulesTests
{
    [Fact]
    public void CalculoVigencia_DeveIdentificarContratoProximoDoVencimento()
    {
        var dataBase = new DateTime(2026, 10, 25);
        var contrato = new Contrato
        {
            NumeroContrato = "CTR-TEST-01",
            Status = StatusContrato.Vigente,
            DataInicioVigencia = dataBase.AddMonths(-11),
            DataTerminoVigencia = dataBase.AddDays(22),
            DiasAlertaVencimento = 60,
            DiasAvisoPrevioRescisao = 30,
            RenovaAutomaticamente = true
        };

        Assert.Equal(22, contrato.DiasRestantes(dataBase));
        Assert.True(contrato.EstaProximoVencimento(dataBase));
        Assert.False(contrato.EstaVencido(dataBase));
        Assert.True(contrato.EmJanelaCriticaAvisoPrevio(dataBase));
    }

    [Fact]
    public void CalculoVigencia_DeveIdentificarContratoVencido()
    {
        var dataBase = new DateTime(2026, 10, 25);
        var contrato = new Contrato
        {
            NumeroContrato = "CTR-TEST-02",
            Status = StatusContrato.Vigente,
            DataInicioVigencia = dataBase.AddMonths(-12),
            DataTerminoVigencia = dataBase.AddDays(-5),
            DiasAlertaVencimento = 60
        };

        Assert.Equal(-5, contrato.DiasRestantes(dataBase));
        Assert.True(contrato.EstaVencido(dataBase));
        Assert.False(contrato.EstaProximoVencimento(dataBase));
    }

    [Fact]
    public void AuditoriaFatura_DeveDetectarDesvioMonetarioEPercentualExato()
    {
        var faturaComDesvio = new FaturaGasto
        {
            ContratoId = 1,
            FornecedorId = 1,
            MesReferencia = 10,
            AnoReferencia = 2026,
            ValorContratado = 50000.00m,
            ValorCobrado = 57500.00m
        };

        Assert.True(faturaComDesvio.PossuiDesvio);
        Assert.Equal(7500.00m, faturaComDesvio.ValorDesvio);
        Assert.Equal(15.00m, faturaComDesvio.PorcentagemDesvio);

        var faturaConforme = new FaturaGasto
        {
            ContratoId = 1,
            FornecedorId = 1,
            MesReferencia = 10,
            AnoReferencia = 2026,
            ValorContratado = 50000.00m,
            ValorCobrado = 50000.00m
        };

        Assert.False(faturaConforme.PossuiDesvio);
        Assert.Equal(0m, faturaConforme.ValorDesvio);
        Assert.Equal(0m, faturaConforme.PorcentagemDesvio);
    }

    [Fact]
    public void ValidadorCnpj_DeveValidarDigitosVerificadoresCorretamente()
    {
        Assert.True(CnpjValidator.EhValido("04.712.500/0001-07"));
        Assert.True(CnpjValidator.EhValido("53113791000122"));
        Assert.False(CnpjValidator.EhValido("11.111.111/1111-11"));
        Assert.False(CnpjValidator.EhValido("04.712.500/0001-99"));
    }
}
