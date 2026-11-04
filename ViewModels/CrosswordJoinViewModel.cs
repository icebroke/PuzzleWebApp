using System.ComponentModel.DataAnnotations;

namespace PuzzleWebApp.ViewModels;

public class CrosswordJoinViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public string CrosswordDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sila masukkan nama anda.")]
    [StringLength(100, ErrorMessage = "Nama anda tidak boleh melebihi 100 aksara.")]
    [Display(Name = "Nama anda")]
    public string PlayerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sila masukkan MyKad anda.")]
    [StringLength(12, MinimumLength = 12, ErrorMessage = "MyKad mesti 12 digit.")]
    [Display(Name = "ID pemain")]
    public string PlayerId { get; set; } = string.Empty;
}
