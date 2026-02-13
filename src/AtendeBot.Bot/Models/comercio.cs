using System.ComponentModel.DataAnnotations.Schema;

namespace AtendeBot.Bot.Models;

[Table("comercios")]
public class Comercio
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }

    [Column("telegram_chat_id")]
    public string? TelegramChatId { get; set; }

    [Column("horario_abertura")]
    public TimeSpan? HorarioAbertura { get; set; }

    [Column("horario_fechamento")]
    public TimeSpan? HorarioFechamento { get; set; }

    public bool Ativo { get; set; }
}