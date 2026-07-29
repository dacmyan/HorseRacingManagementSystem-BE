namespace HorseRacing.Domain.Entities;

public class Wallet
{
    public int WalletId { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public decimal Balance { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
