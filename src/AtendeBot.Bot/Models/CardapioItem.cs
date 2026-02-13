using System.ComponentModel.DataAnnotations.Schema;

namespace AtendeBot.Bot.Models;

[Table("cardapio_itens")]
public class CardapioItem
{
  public int Id { get; set; }

  [Column("comercio_id")]
  public int ComercioId { get; set; }

  public string Nome { get; set; } = string.Empty;

  public string? Descricao { get; set; }

  public decimal Preco { get; set; }

  public string? Categoria { get; set; }

  public bool Disponivel { get; set; }
}