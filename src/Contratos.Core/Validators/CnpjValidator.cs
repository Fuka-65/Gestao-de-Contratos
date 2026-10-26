using System.Text.RegularExpressions;

namespace Contratos.Core.Validators;

public static class CnpjValidator
{
    public static bool EhValido(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return false;

        var digitos = Regex.Replace(cnpj, @"[^\d]", "");
        if (digitos.Length != 14)
            return false;

        // Rejeita sequências repetidas como 00000000000000
        if (digitos.Distinct().Count() == 1)
            return false;

        int[] multiplicador1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multiplicador2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        string tempCnpj = digitos[..12];
        int soma = 0;

        for (int i = 0; i < 12; i++)
            soma += (tempCnpj[i] - '0') * multiplicador1[i];

        int resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        string digito = resto.ToString();
        tempCnpj += digito;
        soma = 0;

        for (int i = 0; i < 13; i++)
            soma += (tempCnpj[i] - '0') * multiplicador2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        digito += resto.ToString();

        return digitos.EndsWith(digito, StringComparison.Ordinal);
    }

    public static string Formatar(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return string.Empty;

        var digitos = Regex.Replace(cnpj, @"[^\d]", "");
        if (digitos.Length != 14)
            return cnpj.Trim();

        return $"{digitos[..2]}.{digitos.Substring(2, 3)}.{digitos.Substring(5, 3)}/{digitos.Substring(8, 4)}-{digitos.Substring(12, 2)}";
    }
}
