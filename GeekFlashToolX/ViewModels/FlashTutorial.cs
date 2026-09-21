namespace GeekFlashToolX.ViewModels;

public sealed record ConnectionTutorial(string Name, string Description, IReadOnlyList<TutorialStep> Steps);

public sealed record TutorialStep(string Index, string Title, string Hint);
