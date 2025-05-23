namespace EMMS.CustomRequiredAttribute
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Reflection;

    public class RequiredIfAttribute : ValidationAttribute
    {
        private string _propertyName;
        private object _desiredValue;
    }

}
