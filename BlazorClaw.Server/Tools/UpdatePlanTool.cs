using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools;

public class UpdatePlanTool : BaseTool<UpdatePlanTool.UpdatePlanParams>
{
    public override string Name => "update_plan";
    public override string Description => "Aktualisiert den strukturierten Arbeitsplan für den aktuellen Run";

    protected override Task<string> ExecuteInternalAsync(UpdatePlanParams p, MessageContext context)
    {
        if (p.Plan == null || p.Plan.Count == 0)
            throw new Exception("Plan darf nicht leer sein.");

        var inProgressCount = 0;

        foreach (var step in p.Plan)
        {
            if (string.IsNullOrWhiteSpace(step.Step))
                throw new Exception("Jeder Planschritt braucht ein step.");

            if (step.Status == PlanState.in_progress)
                inProgressCount++;
        }

        if (inProgressCount > 1)
            throw new Exception("Es darf maximal ein Schritt den Status 'in_progress' haben.");

        return Task.FromResult("Plan aktualisiert.");
    }

    public class UpdatePlanStepParams
    {
        [Description("Kurze Beschreibung des Planschritts")]
        [Required]
        public string Step { get; set; } = string.Empty;

        [Description("Status des Schritts: pending, in_progress oder completed")]
        [Required]
        public PlanState Status { get; set; }
    }

    public class UpdatePlanParams
    {
        [Description("Optionale kurze Notiz, was sich am Plan geändert hat")]
        public string? Explanation { get; set; }

        [Description("Liste der Planschritte")]
        [Required]
        [MinLength(1)]
        public List<UpdatePlanStepParams> Plan { get; set; } = [];
    }
    public enum PlanState
    {
        pending,
        in_progress,
        completed
    }
}

