using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed class SchoolAccessOptions
{
    public Guid[] PlatformOperatorUserIds { get; set; } = [];
    public Guid[] LessonCorrectionUserIds { get; set; } = [];
}
