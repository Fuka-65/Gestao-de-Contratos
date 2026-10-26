using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Services;
using Contratos.Core.Validators;
using Contratos.Core.ViewModels;
using Contratos.Infrastructure.Persistence;
using Contratos.Infrastructure.Security;
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

    [Fact]
    public void OrcamentoEImpostos_DeveCalcularBrutoLiquidoImpostosEOpexCapexCorretamente()
    {
        var contrato = new Contrato
        {
            NumeroContrato = "CTR-ORC-01",
            Status = StatusContrato.Vigente,
            DataInicioVigencia = new DateTime(2026, 1, 1),
            DataTerminoVigencia = new DateTime(2026, 12, 31),
            ValorMensalContratado = 100000.00m,
            ValorOpexMensal = 70000.00m,
            ValorCapexMensal = 30000.00m,
            AliquotaImpostosPercentual = 15.00m,
            IndiceReajuste = IndiceReajuste.IPCA,
            PeriodicidadeReajusteMeses = 12,
            RenovaAutomaticamente = true
        };

        // 1. Verificação mensal direta
        Assert.Equal(70000.00m, contrato.ObterOpexEfetivo());
        Assert.Equal(30000.00m, contrato.ObterCapexEfetivo());
        Assert.Equal(15000.00m, contrato.CalcularImpostosMensais());
        Assert.Equal(85000.00m, contrato.CalcularValorLiquidoMensal());

        // 2. Projeção para 6 meses (Semestral) sem reajuste
        decimal somaBrutoSemestral = 0m;
        decimal somaImpostosSemestral = 0m;
        decimal somaLiquidoSemestral = 0m;

        for (int mes = 1; mes <= 6; mes++)
        {
            var proj = contrato.ProjetarCompetencia(2026, mes, considerarRenovacaoAutomatica: true, percentualReajusteAnualSimulado: 0m);
            Assert.True(proj.AtivoNoMes);
            somaBrutoSemestral += proj.ValorBruto;
            somaImpostosSemestral += proj.ValorImpostos;
            somaLiquidoSemestral += proj.ValorLiquido;
        }

        Assert.Equal(600000.00m, somaBrutoSemestral);
        Assert.Equal(90000.00m, somaImpostosSemestral);
        Assert.Equal(510000.00m, somaLiquidoSemestral);
    }

    [Fact]
    public async Task ImportacaoCsvFornecedores_DeveControlarDuplicidadeEDetectarDivergenciasDeCampos()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clm_csv_import_{Guid.NewGuid():N}.json");
        try
        {
            var crypto = new AesCryptoService();
            var repo = new JsonClmRepository(tempFile, crypto);

            var csvLinhas = string.Join("\r\n", new[]
            {
                FornecedorCsvService.CabecalhoPadrao,
                // Linha 2: Novo fornecedor válido (Equinix - 33.000.167/0001-01)
                "33.000.167/0001-01;Equinix Brasil S.A.;Equinix SP4;112.440;4.110;Gustavo;(11) 3500-8000;g@equinix.com;Fin;(11) 3500-8010;f@equinix.com;NOC;99.99%;https://equinix.com.br;Sim;Novo data center",
                // Linha 3: Repetido dentro do mesmo CSV
                "33.000.167/0001-01;Equinix Brasil S.A.;Equinix SP4;112.440;4.110;Gustavo;(11) 3500-8000;g@equinix.com;Fin;(11) 3500-8010;f@equinix.com;NOC;99.99%;https://equinix.com.br;Sim;Repetido no mesmo CSV",
                // Linha 4: Já cadastrado na Seed (Microsoft - 04.712.500/0001-07) com divergência de Nome Fantasia e Telefone
                "04.712.500/0001-07;Microsoft Informática Ltda;Microsoft Cloud Azure Brasil;116.011.448.118;3.049.211-8;Ricardo Vasconcelos (Enterprise Account Exec);(11) 99999-0000;ricardo.vasconcelos@microsoft-partner.com;Faturamento Enterprise Brasil;0800 888 4081;billing.br@microsoft.com;Premier Support 24x7 - Portal Azure/M365;Crítico (Sev A): 1 hora | Alta (Sev B): 4 horas;https://www.microsoft.com/pt-br;Sim;Contrato Enterprise Agreement (EA) para licenciamento Microsoft 365 E5, Dynamics e Power BI Pro.",
                // Linha 5: CNPJ Inválido
                "11.111.111/1111-11;Fornecedor Invalido;Fantasia Invalida;;;;;;;;;;;;Sim;Erro"
            });

            var resultado = await FornecedorCsvService.ProcessarArquivoCsvAsync(csvLinhas, "teste_lote.csv", repo);

            Assert.Equal(4, resultado.TotalLinhasProcessadas);
            Assert.Equal(1, resultado.TotalNovosInseridos);
            Assert.Equal(1, resultado.TotalDuplicadosNoArquivo);
            Assert.Equal(1, resultado.TotalConflitosPendentes);
            Assert.Equal(1, resultado.TotalErrosValidacao);

            var conflito = resultado.Itens.Single(i => i.Status == StatusLinhaImportacaoFornecedor.ConflitoDivergenciaPendente);
            Assert.Equal("04.712.500/0001-07", conflito.Cnpj);
            Assert.Contains(conflito.Divergencias, d => d.NomeCampo == "Nome Fantasia" && d.NovoValorCsv == "Microsoft Cloud Azure Brasil");
            Assert.Contains(conflito.Divergencias, d => d.NomeCampo == "Contato Comercial (Telefone)" && d.NovoValorCsv == "(11) 99999-0000");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
