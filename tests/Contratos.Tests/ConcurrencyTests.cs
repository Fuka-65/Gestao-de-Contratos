using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Infrastructure.Persistence;
using Contratos.Infrastructure.Security;
using Xunit;

namespace Contratos.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task RepositorioJson_MultiplasThreadsParalelas_DeveGarantirAtomicidadeSemPerdaDeDados()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clm_concurrency_{Guid.NewGuid():N}.json");
        try
        {
            var crypto = new AesCryptoService();
            var repo = new JsonClmRepository(tempFile, crypto);

            var contratosIniciais = (await repo.ObterContratosAsync()).Count;
            const int totalThreads = 25;

            var tasks = Enumerable.Range(1, totalThreads).Select(i => Task.Run(async () =>
            {
                var novoContrato = new Contrato
                {
                    NumeroContrato = $"CTR-CONC-{i:D4}",
                    TituloObjeto = $"Contrato Concorrente de Teste #{i}",
                    FornecedorId = 1,
                    CategoriaContrato = CategoriaContrato.ServicosTI,
                    DataAssinatura = DateTime.Today,
                    DataInicioVigencia = DateTime.Today,
                    DataTerminoVigencia = DateTime.Today.AddMonths(12),
                    ValorMensalContratado = 1000m * i,
                    ValorGlobalContrato = 12000m * i,
                    CentroDeCusto = $"CC-{i:D3}",
                    DepartamentoResponsavel = "TI",
                    GestorContratoNome = $"Gestor {i}",
                    GestorContratoEmail = $"gestor{i}@empresa.com.br",
                    Status = StatusContrato.Vigente
                };

                await repo.SalvarContratoAsync(novoContrato);

                // Realiza leituras simultâneas durante a carga de escrita
                var leitura = await repo.ObterContratosAsync();
                Assert.NotEmpty(leitura);
            }));

            await Task.WhenAll(tasks);

            var contratosFinais = await repo.ObterContratosAsync();
            Assert.Equal(contratosIniciais + totalThreads, contratosFinais.Count);

            for (int i = 1; i <= totalThreads; i++)
            {
                Assert.Contains(contratosFinais, c => c.NumeroContrato == $"CTR-CONC-{i:D4}");
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
