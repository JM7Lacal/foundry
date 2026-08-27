using Foundry.Application.Validation;
using Foundry.Core.Content;

namespace Foundry.Presentation.ViewModels;

/// <summary>Una fila del panel de validacion. Doble click navega a la entidad.</summary>
public sealed class ValidationIssueViewModel
{
    private readonly ValidationIssue _issue;

    public ValidationIssueViewModel(ValidationIssue issue)
    {
        _issue = issue;
    }

    public EntityId EntityId => _issue.EntityId;

    public bool IsError => _issue.Severity == ValidationSeverity.Error;

    public string Text => $"{_issue.EntityName} · {_issue.Field}: {_issue.Message}";
}
