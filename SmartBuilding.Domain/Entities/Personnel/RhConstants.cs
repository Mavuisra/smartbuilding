namespace SmartBuilding.Domain.Entities.Personnel;

public static class RhConstants
{
    public static readonly TimeSpan WorkDayStart = TimeSpan.FromHours(8);
    public static readonly TimeSpan WorkDayEnd = TimeSpan.FromHours(17);
    public const double StandardWorkHours = 9;

    public static class EmployeeStatus
    {
        public const string Active = "Actif";
        public const string Suspended = "Suspendu";
        public const string OnLeave = "Congé";
        public const string Dismissed = "Renvoyé";
        public const string Pending = "En attente";
    }

    public static class PresenceStatus
    {
        public const string Present = "Présent";
        public const string Late = "Retard";
        public const string Absent = "Absent";
        public const string Leave = "Congé";
        public const string EarlyLeave = "Sortie anticipée";
        public const string NotChecked = "Non pointé";
        public const string Inactive = "Inactif";
    }

    public static class PayrollStatus
    {
        public const string Pending = "En attente";
        public const string Validated = "Validé";
        public const string Paid = "Payé";
    }

    public static class DisciplinaryCategory
    {
        public const string Warning = "Avertissement";
        public const string Remark = "Remarque RH";
        public const string Incident = "Incident";
        public const string Behavior = "Comportement";
        public const string Performance = "Performance";
        public const string Suspension = "Suspension";
    }
}
