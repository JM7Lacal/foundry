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
    private ContentEntity? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeEntityCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExplainUpgradesCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckBalanceCommand))]
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

    public void SetContext(ContentDatabase database)
    {
        _context = database;
        AskCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Le dice al asistente que entidad esta abierta, para las acciones rapidas.</summary>
    public void SetSelectedEntity(ContentEntity? entity)
    {
        _selected = entity;
        AnalyzeEntityCommand.NotifyCanExecuteChanged();
        ExplainUpgradesCommand.NotifyCanExecuteChanged();
        CheckBalanceCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRunEntityAction))]
    private Task AnalyzeEntity() => RunQuickAction(
        $"Analizá «{_selected!.Name}» ({_selected.Id}): revisá sus stats contra entidades similares y "
        + "marcá cualquier valor sospechoso o incoherente. Respondé solo con texto en \"answer\", sin crear entidades.");

    [RelayCommand(CanExecute = nameof(CanRunEntityAction))]
    private Task ExplainUpgrades() => RunQuickAction(
        $"Explicá la cadena de mejora de «{_selected!.Name}» ({_selected.Id}): que mejora a que, si la "
        + "progresion de daño/vida/costo es coherente, y que ajustarias. Solo texto en \"answer\".");

    [RelayCommand(CanExecute = nameof(CanRunEntityAction))]
    private Task CheckBalance() => RunQuickAction(
        $"¿«{_selected!.Name}» ({_selected.Id}) esta balanceada frente a los enemigos del juego? Si ves un "
        + "problema, devolvé la entidad ajustada en \"entities\" (con el mismo id) para que el usuario la revise, "
        + "y explicá el cambio en \"rationale\".");

    // Las acciones rapidas traen su propio prompt, asi que no exigen texto escrito.
    private bool CanRunEntityAction() => _selected is not null && !IsBusy && _context is not null;

    private async Task RunQuickAction(string prompt)
    {
        Prompt = prompt;
        if (AskCommand.CanExecute(null))
        {
            await AskCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

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

            SetProposal(result.ProposedEntities);

            // Con propuesta: el rationale explica el cambio. Sin propuesta: una sola respuesta,
            // no rationale + answer diciendo lo mismo.
            if (result.HasProposal)
            {
                Rationale = result.Rationale ?? result.Answer;
                Answer = null;
            }
            else
            {
                Answer = result.Answer ?? result.Rationale;
                Rationale = null;
            }
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
