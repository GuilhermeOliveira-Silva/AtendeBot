using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtendeBot.Bot.Models;

[Table("pedidos")]
public class Pedido
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("comercio_id")]
    public int ComercioId { get; set; }

    [Column("cliente_telegram_id")]
    public string ClienteTelegramId { get; set; } = "";

    [Column("tipo_entrega")]
    public string? TipoEntrega { get; set; }

    [Column("endereco_entrega")]
    public string? EnderecoEntrega { get; set; }

    [Column("observacao")]
    public string? Observacao { get; set; }  // ← NOVO!

    [Column("status")]
    public string Status { get; set; } = "novo";

    [Column("total")]
    public decimal Total { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; }
}