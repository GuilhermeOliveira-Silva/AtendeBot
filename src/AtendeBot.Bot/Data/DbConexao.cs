using Microsoft.EntityFrameworkCore;
using AtendeBot.Bot.Models;

namespace AtendeBot.Bot.Data;

public class AppDbContext : DbContext
{
    public DbSet<Comercio> Comercios { get; set; }
    public DbSet<CardapioItem> CardapioItens { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<PedidoItem> PedidoItens { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}