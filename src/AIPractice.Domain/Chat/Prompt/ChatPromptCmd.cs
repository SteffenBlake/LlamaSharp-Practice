using System.ComponentModel.DataAnnotations;
using AIPractice.Domain.Validation;
using Validly;

namespace AIPractice.Domain.Chat.Prompt;

[Validatable]
public partial class ChatPromptCmd : IDomainValidatable
{
    [Required]
    public required string Prompt { get; set; }
}
