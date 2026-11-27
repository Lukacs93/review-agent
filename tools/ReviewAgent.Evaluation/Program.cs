using System.CommandLine;
using ReviewAgent.Evaluation;

// The fewest runs that show how much the agent's answers vary from run to run.
const int DefaultRuns = 3;

// After Ctrl+C the tool stops the services it started and deletes its temporary folder, which takes a few seconds.
// System.CommandLine ends the process after 2 seconds by default, which could leave both behind; this leaves room
// for a slow machine.
var invocation = new InvocationConfiguration { ProcessTerminationTimeout = TimeSpan.FromSeconds(30) };

var executionOption = new Option<string?>("--execution")
{
    Description = "The measurement's name. A new name starts a measurement; the name of one that stopped continues "
        + "it. By default the start time.",
};
var runsOption = new Option<int>("--runs")
{
    Description = "How many times each task is reviewed, in a new measurement.",
    DefaultValueFactory = _ => DefaultRuns,
};
var continueOption = new Option<bool>("--continue-despite-changes")
{
    Description = "Continue a measurement although review-agent's commit changed since it started.",
};

var rootCommand = new RootCommand("Measures how well review-agent reviews the evaluation tasks.")
{
    executionOption,
    runsOption,
    continueOption,
};
rootCommand.SetAction((parseResult, cancellationToken) => EvaluationCommand.RunAsync(
    new EvaluationArguments(
        parseResult.GetValue(executionOption),
        parseResult.GetValue(runsOption),
        parseResult.GetValue(continueOption)),
    Console.Out,
    cancellationToken));

return await rootCommand.Parse(args).InvokeAsync(invocation);
