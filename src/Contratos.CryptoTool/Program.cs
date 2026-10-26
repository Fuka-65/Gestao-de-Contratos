using Contratos.Infrastructure.Security;

namespace Contratos.CryptoTool;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine(" CONTRATOS.CRYPTOTOOL - UTILITÁRIO DE CRIPTOGRAFIA CORPORATIVA AES-256");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        var crypto = new AesCryptoService();

        if (args.Length > 0)
        {
            var comando = args[0].ToLowerInvariant();
            if (comando == "--decrypt" || comando == "-d")
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Uso: Contratos.CryptoTool --decrypt \"{ENC:...}\"");
                    return 1;
                }
                var plain = crypto.Decrypt(args[1]);
                Console.WriteLine($"Valor Decriptado: {plain}");
                return 0;
            }
            else if (comando == "--hash" || comando == "-h")
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Uso: Contratos.CryptoTool --hash \"SuaSenhaForte\"");
                    return 1;
                }
                var hash = crypto.HashPassword(args[1]);
                Console.WriteLine($"Hash PBKDF2-SHA256: {hash}");
                return 0;
            }
            else
            {
                var input = args[0];
                var token = crypto.EncryptWithToken(input);
                Console.WriteLine($"Texto Original : {input}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Token Gerado   : {token}");
                Console.ResetColor();
                return 0;
            }
        }

        Console.WriteLine("Digite a senha ou string de conexão que deseja proteger com AES-256:");
        Console.Write("> ");
        var secret = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(secret))
        {
            Console.WriteLine("Nenhum valor informado. Operação encerrada.");
            return 0;
        }

        var encryptedToken = crypto.EncryptWithToken(secret);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Token Criptografado (copie para o appsettings.json):");
        Console.WriteLine(encryptedToken);
        Console.ResetColor();
        return 0;
    }
}
