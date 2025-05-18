using Validly;

namespace AIPractice.Domain.Validation;

public interface IDomainValidatable 
{
    ValidationResult Validate();
}
