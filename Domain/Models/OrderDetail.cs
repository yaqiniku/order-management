namespace Domain.Models;

public class OrderDetail
{
    public string? ID { get; set; }
    public string? OrderID { get; set; }
    public string? ProductID { get; set; }
    public int? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? CreDate { get; set; }
    public DateTime? ModDate { get; set; }
}
