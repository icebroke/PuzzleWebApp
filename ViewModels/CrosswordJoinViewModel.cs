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

    [Required(ErrorMessage = "Sila masukkan ID pemain.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "ID pemain tidak boleh melebihi 80 aksara.")]
    [Display(Name = "ID pemain")]
    public string PlayerId { get; set; } = string.Empty;
}
