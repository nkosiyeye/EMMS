using System.ComponentModel.DataAnnotations;
public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public RequiredIfAttribute(string dependentProperty, object targetValue)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(_dependentProperty);
        if (property == null)
            return new ValidationResult($"Invalid status {_dependentProperty}");

        var dependentValue = property.GetValue(validationContext.ObjectInstance);

        if (dependentValue != null && dependentValue.Equals(_targetValue))
        {
            if (value == null || (value is int val && val < 0) )
            {
                return new ValidationResult(ErrorMessage);
            }
        }

        return ValidationResult.Success;
    }
}
