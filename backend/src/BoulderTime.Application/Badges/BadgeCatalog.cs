namespace BoulderTime.Application.Badges;

public enum BadgeCategory { Progression = 0, Consistency = 1, Experience = 2, Exploration = 3, Community = 4, Competition = 5, Special = 6 }

/// <summary>What earns a badge.</summary>
public enum BadgeRule
{
    /// <summary>Boulders completed, anywhere, ever.</summary>
    Completions,
    /// <summary>Attempts recorded, all boulders together.</summary>
    Attempts,
    /// <summary>Distinct days (Italian time) with climbing activity.</summary>
    ActiveDays,
    /// <summary>Gyms where the person completed at least one boulder (the rule of ADR-064).</summary>
    Gyms,
    /// <summary>A boulder completed after it was marked as a project.</summary>
    ProjectClosed,
    /// <summary>Comments written.</summary>
    Comments,
    /// <summary>Community grade votes.</summary>
    GradeVotes,
    /// <summary>Champion of a gym's month (points leaderboard), once per gym and month.</summary>
    MonthChampion,
    /// <summary>Champion of a gym's year, once per gym and year.</summary>
    YearChampion,
    /// <summary>A number of month titles at the same gym; once per gym.</summary>
    MonthTitlesAtGym,
    /// <summary>A number of year titles at the same gym; once per gym.</summary>
    YearTitlesAtGym,
    /// <summary>Climbed at a gym before its beta test closed (ADR-069).</summary>
    BetaTester,
    /// <summary>Given by hand, from the server only (<c>grant-badge</c>).</summary>
    Manual,
}

/// <param name="Id">Stable: stored with every award, never renamed.</param>
/// <param name="Art">Key of the picture the app shows; replacing the picture never touches the badge.</param>
/// <param name="Enabled">
/// Off: the badge is described but nobody earns it, and nobody sees it, until it is switched on.
/// </param>
public sealed record BadgeDefinition(
    string Id, BadgeCategory Category, BadgeRule Rule, int Threshold, string Art,
    string NameIt, string NameEn, string DescriptionIt, string DescriptionEn, bool Enabled = true)
{
    public string Name(string language) => language == Localization.Language.English ? NameEn : NameIt;
    public string Description(string language) => language == Localization.Language.English ? DescriptionEn : DescriptionIt;
}

/// <summary>
/// Every badge BoulderTime knows (ADR-069), in the order the app lists them within a category. Adding one is adding a
/// line; ids and art keys never change once released.
/// </summary>
public static class BadgeCatalog
{
    public static readonly IReadOnlyList<BadgeDefinition> All =
    [
        new("PROGRESSION_COMPLETIONS_1", BadgeCategory.Progression, BadgeRule.Completions, 1, "progression-1",
            "Primo blocco", "First boulder", "Completa il tuo primo blocco.", "Complete your first boulder."),
        new("PROGRESSION_COMPLETIONS_10", BadgeCategory.Progression, BadgeRule.Completions, 10, "progression-10",
            "Inizio della scalata", "Off the ground", "Completa 10 blocchi.", "Complete 10 boulders."),
        new("PROGRESSION_COMPLETIONS_50", BadgeCategory.Progression, BadgeRule.Completions, 50, "progression-50",
            "Scalatore esperto", "Seasoned climber", "Completa 50 blocchi.", "Complete 50 boulders."),
        new("PROGRESSION_COMPLETIONS_100", BadgeCategory.Progression, BadgeRule.Completions, 100, "progression-100",
            "Veterano", "Veteran", "Completa 100 blocchi.", "Complete 100 boulders."),
        new("PROGRESSION_COMPLETIONS_500", BadgeCategory.Progression, BadgeRule.Completions, 500, "progression-500",
            "Leggenda", "Legend", "Completa 500 blocchi.", "Complete 500 boulders."),

        new("CONSISTENCY_DAYS_3", BadgeCategory.Consistency, BadgeRule.ActiveDays, 3, "consistency-3",
            "Presenza costante", "Regular", "Scala in 3 giorni diversi.", "Climb on 3 different days."),
        new("CONSISTENCY_DAYS_10", BadgeCategory.Consistency, BadgeRule.ActiveDays, 10, "consistency-10",
            "Abitué della parete", "Wall regular", "Scala in 10 giorni diversi.", "Climb on 10 different days."),
        new("CONSISTENCY_DAYS_30", BadgeCategory.Consistency, BadgeRule.ActiveDays, 30, "consistency-30",
            "Parte della community", "Part of the community", "Scala in 30 giorni diversi.", "Climb on 30 different days."),
        new("CONSISTENCY_DAYS_100", BadgeCategory.Consistency, BadgeRule.ActiveDays, 100, "consistency-100",
            "Sempre in parete", "Always on the wall", "Scala in 100 giorni diversi.", "Climb on 100 different days."),

        new("EXPERIENCE_ATTEMPTS_10", BadgeCategory.Experience, BadgeRule.Attempts, 10, "experience-attempts-10",
            "Primi tentativi", "First tries", "Registra 10 tentativi.", "Record 10 attempts."),
        new("EXPERIENCE_ATTEMPTS_50", BadgeCategory.Experience, BadgeRule.Attempts, 50, "experience-attempts-50",
            "Perseveranza", "Perseverance", "Registra 50 tentativi.", "Record 50 attempts."),
        new("EXPERIENCE_ATTEMPTS_100", BadgeCategory.Experience, BadgeRule.Attempts, 100, "experience-attempts-100",
            "Tenacia", "Tenacity", "Registra 100 tentativi.", "Record 100 attempts."),
        new("EXPERIENCE_PROJECT_CLOSED", BadgeCategory.Experience, BadgeRule.ProjectClosed, 1, "experience-project-closed",
            "Progetto chiuso", "Project closed", "Completa un blocco che avevi tra i tuoi progetti.", "Complete a boulder you had among your projects."),

        new("EXPLORATION_GYMS_1", BadgeCategory.Exploration, BadgeRule.Gyms, 1, "exploration-1",
            "Prima palestra", "First gym", "Completa un blocco nella tua prima palestra.", "Complete a boulder at your first gym."),
        new("EXPLORATION_GYMS_3", BadgeCategory.Exploration, BadgeRule.Gyms, 3, "exploration-3",
            "Esploratore", "Explorer", "Completa blocchi in 3 palestre.", "Complete boulders at 3 gyms."),
        new("EXPLORATION_GYMS_5", BadgeCategory.Exploration, BadgeRule.Gyms, 5, "exploration-5",
            "Viaggiatore", "Traveller", "Completa blocchi in 5 palestre.", "Complete boulders at 5 gyms."),
        new("EXPLORATION_GYMS_10", BadgeCategory.Exploration, BadgeRule.Gyms, 10, "exploration-10",
            "Esploratore instancabile", "Tireless explorer", "Completa blocchi in 10 palestre.", "Complete boulders at 10 gyms."),

        // Described, not live yet: switched on with their pictures.
        new("COMMUNITY_FIRST_COMMENT", BadgeCategory.Community, BadgeRule.Comments, 1, "community-first-comment",
            "Prima parola", "First word", "Scrivi il tuo primo commento.", "Write your first comment.", Enabled: false),
        new("COMMUNITY_GRADE_VOTES_10", BadgeCategory.Community, BadgeRule.GradeVotes, 10, "community-grade-votes-10",
            "Occhio al grado", "Grade spotter", "Proponi il grado di 10 blocchi.", "Suggest the grade of 10 boulders.", Enabled: false),
        new("COMMUNITY_GRADE_VOTES_50", BadgeCategory.Community, BadgeRule.GradeVotes, 50, "community-grade-votes-50",
            "Voce della community", "Voice of the community", "Proponi il grado di 50 blocchi.", "Suggest the grade of 50 boulders.", Enabled: false),
        new("COMMUNITY_GRADE_VOTES_100", BadgeCategory.Community, BadgeRule.GradeVotes, 100, "community-grade-votes-100",
            "Giudice della parete", "Wall judge", "Proponi il grado di 100 blocchi.", "Suggest the grade of 100 boulders.", Enabled: false),

        new("COMPETITION_MONTH_CHAMPION", BadgeCategory.Competition, BadgeRule.MonthChampion, 1, "competition-month",
            "Campione del mese", "Champion of the month", "Primo posto nella classifica mensile di una palestra.", "First place in a gym's monthly leaderboard."),
        new("COMPETITION_YEAR_CHAMPION", BadgeCategory.Competition, BadgeRule.YearChampion, 1, "competition-year",
            "Campione dell'anno", "Champion of the year", "Primo posto nella classifica annuale di una palestra.", "First place in a gym's yearly leaderboard."),
        new("COMPETITION_DOUBLE", BadgeCategory.Competition, BadgeRule.MonthTitlesAtGym, 2, "competition-double",
            "Doppietta", "Double", "Primo posto in 2 mesi distinti nella stessa palestra.", "First place in 2 different months at the same gym."),
        new("COMPETITION_DOMINATOR", BadgeCategory.Competition, BadgeRule.MonthTitlesAtGym, 3, "competition-dominator",
            "Dominatore", "Dominator", "Primo posto in 3 mesi distinti nella stessa palestra.", "First place in 3 different months at the same gym."),
        new("COMPETITION_GYM_LEGEND", BadgeCategory.Competition, BadgeRule.YearTitlesAtGym, 2, "competition-gym-legend",
            "Leggenda della palestra", "Gym legend", "Primo posto in 2 classifiche annuali nella stessa palestra.", "First place in 2 yearly leaderboards at the same gym."),

        new("SPECIAL_BETA_TESTER", BadgeCategory.Special, BadgeRule.BetaTester, 1, "special-beta-tester",
            "Beta Tester", "Beta Tester", "Hai contribuito alla crescita di BoulderTime partecipando alla fase di test.",
            "You helped BoulderTime grow by taking part in its test phase."),
        new("SPECIAL_FOUNDER", BadgeCategory.Special, BadgeRule.Manual, 1, "special-founder",
            "Fondatore di BoulderTime", "BoulderTime founder", "Il creatore di BoulderTime e colui che ha dato inizio alla sua storia.",
            "The creator of BoulderTime, who started its story."),
    ];

    private static readonly Dictionary<string, BadgeDefinition> ById = All.ToDictionary(b => b.Id);

    public static BadgeDefinition? Find(string id) => ById.GetValueOrDefault(id);

    public static IEnumerable<BadgeDefinition> Live(BadgeRule rule) => All.Where(b => b.Enabled && b.Rule == rule);
}
