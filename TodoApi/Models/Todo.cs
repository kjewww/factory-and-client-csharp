using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }

    [Required]
    [MinLength(3)]
    public string Title { get; set; } = "";

    public bool IsCompleted { get; set; } = false;
}