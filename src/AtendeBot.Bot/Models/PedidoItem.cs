using System.ComponentModel.DataAnnotations.Schema;

namespace AtendeBot.Bot.Models;

[Table("pedido_itens")]
public class PedidoItem
{
    public int Id { get; set; }

    [Column("pedido_id")]
    public int PedidoId { get; set; }

    [Column("cardapio_item_id")]
    public int CardapioItemId { get; set; }

    public int Quantidade { get; set; }

    [Column("preco_unitario")]
    public decimal PrecoUnitario { get; set; }
}