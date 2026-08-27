using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foundry.Application.Ai;
using Foundry.Application.Content;
using Foundry.Core.Content;

namespace Foundry.Presentation.ViewModels;

/// <summary>
/// Panel del asistente. Manda el pedido del usuario al <see cref="ContentAssistant"/> (que a su
/// vez usa el proveedor configurado) y muestra la respuesta y/o las entidades propuestas.
/// Aplicar una propuesta lo hace el <see cref="MainViewModel"/> via el evento
/// <see cref="ProposalApplied"/> — pasa por el undo stack.
/// </summary>
public partial class AssistantViewModel : ObservableObject
{
    private readonly ContentAssistant _assistant;

    private ContentDatabase? _context;

    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _answer;

    [ObservableProperty]
    private string? _rationale;

    [ObservableProperty]
    private string? _errorMessage;

    public AssistantViewModel(ContentAssistant assistant)
    {
        _assistant = assistant;
    }

    /// <summary>Se dispara cuando el usuario aplica la propuesta. El <see cref="MainViewModel"/> la agrega.</summary>
    public event EventHandler? ProposalApplied;

    public string ProviderName => _assistant.ProviderName;

    public ObservableCollection<string> ProposalPreview { get; } = [];

    public IReadOnlyList<ContentEntity> Proposal { get; private set; } = [];

    public bool HasProposal => Proposal.Count > 0;

    public void SetContext(ContentDatabase database) => _context = database;

    [RelayCommand(CanExecute = nameof(CanAsk), IncludeCancelCommand = true)]
    private async Task AskAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        Answer = null;
        Rationale = null;
        ClearProposal();

        try
        {
            var result = await _assistant.AskAsync(Prompt, _context!, cancellationToken).ConfigureAwait(true);

            Answer = result.Answer;
            Rationale = result.Rationale;
            SetProposal(result.ProposedEntities);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Cancelado.";
        }
        catch (ChatCompletionException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (ContentRepositoryException ex)
        {
            ErrorMessage = $"El modelo devolvio contenido invalido: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanAsk() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt) && _context is not null;

    [RelayCommand(CanExecute = nameof(HasProposal))]
    private void ApplyProposal()
    {
        ProposalApplied?.Invoke(this, EventArgs.Empty);
        Answer = $"Agregadas {Proposal.Count} entidad(es) al contenido.";
        ClearProposal();
    }

    private void SetProposal(IReadOnlyList<ContentEntity> entities)
    {
        Proposal = entities;
        ProposalPreview.Clear();
        foreach (var entity in entities)
        {
            ProposalPreview.Add($"{entity.CategoryName}: {entity.Name} ({entity.Id})");
        }

        OnPropertyChanged(nameof(HasProposal));
        ApplyProposalCommand.NotifyCanExecuteChanged();
    }

    private void ClearProposal()
    {
        Proposal = [];
        ProposalPreview.Clear();
        OnPropertyChanged(nameof(HasProposal));
        ApplyProposalCommand.NotifyCanExecuteChanged();
    }
}
