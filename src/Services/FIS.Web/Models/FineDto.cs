namespace FIS.Web.Models;

public class FineDto
{
    public int Fine_code { get; set; }
    public int? vmf_code { get; set; }
    public DateTime? Offence_date { get; set; }
    public string? Offence_reference { get; set; }
    public string? Offence_issuer { get; set; }
    public decimal? Fine_amount { get; set; }
    public DateTime? Appear_date { get; set; }
    public DateTime? Receive_gg_date { get; set; }
    public DateTime? Notify_dept_date { get; set; }
    public short? Site_code { get; set; }
    public string? Offence_name { get; set; }
    public DateTime? Fine_pay_date { get; set; }
    public DateTime? Withdraw_date { get; set; }
    public DateTime? Pay_due_date { get; set; }
    public DateTime? Issuer_notify_date { get; set; }
    public string? Dept_person_name { get; set; }
    public string? Dept_person_id { get; set; }
    public string? Document_type { get; set; }
    public short? Traffic_dept_code { get; set; }
}
