using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Contratos.Core.Enums;

public static class EnumExtensions
{
    public static string ObterNomeExibicao(this Enum valorEnum)
    {
        var member = valorEnum.GetType().GetMember(valorEnum.ToString()).FirstOrDefault();
        if (member != null)
        {
            var displayAttr = member.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null && !string.IsNullOrWhiteSpace(displayAttr.Name))
            {
                return displayAttr.Name;
            }
        }
        return valorEnum.ToString();
    }
}
