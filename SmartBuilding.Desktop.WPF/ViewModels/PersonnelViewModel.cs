using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PersonnelViewModel : BaseViewModel
{
    private readonly PersonnelService _personnelService;
    private readonly ISyncService _syncService;
    private readonly SessionService _session;
    private List<PersonnelEmployeeItem> _allEmployees = [];

    public const string AllDepartments = "Tous départements";
    public const string AllPositions = "Toutes fonctions";
    public const string AllStatuses = "Tous statuts";
    public const string AllPresences = "Présence du jour : Tous";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AP";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterDepartment = AllDepartments;
    [ObservableProperty] private string _filterPosition = AllPositions;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterPresence = AllPresences;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private bool _isEmployeeEditorOpen;
    [ObservableProperty] private bool _isEmployeeDetailPageOpen;
    [ObservableProperty] private bool _isEditingEmployee;
    [ObservableProperty] private string _employeeDetailSubtitle = string.Empty;
    [ObservableProperty] private Guid _editingEmployeeId;
    [ObservableProperty] private bool _isPointagePopupOpen;
    [ObservableProperty] private PersonnelEmployeeItem? _pointageEmployee;
    [ObservableProperty] private string _pointageLeaveReason = string.Empty;
    [ObservableProperty] private string? _pointageError;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;

    [ObservableProperty] private string _formMatricule = string.Empty;
    [ObservableProperty] private string _formFirstName = string.Empty;
    [ObservableProperty] private string _formLastName = string.Empty;
    [ObservableProperty] private string _formPosition = string.Empty;
    [ObservableProperty] private string _formDepartment = string.Empty;
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formBaseSalaryText = "0";
    [ObservableProperty] private DateTime _formHireDate = DateTime.Today;
    [ObservableProperty] private bool _formIsActive = true;
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private int _totalEmployees;
    [ObservableProperty] private int _presentToday;
    [ObservableProperty] private string _presentPercent = "0%";
    [ObservableProperty] private int _absentToday;
    [ObservableProperty] private string _absentPercent = "0%";
    [ObservableProperty] private int _onLeaveToday;
    [ObservableProperty] private string _onLeavePercent = "0%";
    [ObservableProperty] private int _lateToday;
    [ObservableProperty] private string _presenceRateDisplay = "0%";
    [ObservableProperty] private string _monthlyPayroll = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _rentCollectedTotalDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private int _newThisMonth;
    [ObservableProperty] private PersonnelEmployeeItem? _selectedEmployee;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isEmployeeDetailEditMode;

    [ObservableProperty] private string _detailInitials = "E";
    [ObservableProperty] private string _detailSummaryLine = string.Empty;
    [ObservableProperty] private string _detailPhone = "—";
    [ObservableProperty] private string _detailEmail = "—";
    [ObservableProperty] private string _detailAddress = "—";
    [ObservableProperty] private string _detailGender = "—";
    [ObservableProperty] private string _detailBirthDateDisplay = "—";
    [ObservableProperty] private string _detailAgeDisplay = "—";
    [ObservableProperty] private string _detailNationalId = "—";
    [ObservableProperty] private string _detailMaritalStatus = "—";
    [ObservableProperty] private string _detailEmergencyName = "—";
    [ObservableProperty] private string _detailEmergencyPhone = "—";
    [ObservableProperty] private string _detailNotes = "—";
    [ObservableProperty] private string _detailHireDateDisplay = "—";
    [ObservableProperty] private string _detailSupervisor = "—";
    [ObservableProperty] private string _detailWorkSchedule = "—";
    [ObservableProperty] private string _detailSalaryDisplay = "—";
    [ObservableProperty] private string _detailContractNumber = "—";
    [ObservableProperty] private string _detailContractType = "—";
    [ObservableProperty] private string _detailContractStartDisplay = "—";
    [ObservableProperty] private string _detailContractEndDisplay = "—";
    [ObservableProperty] private string _detailContractStatus = "—";
    [ObservableProperty] private string _detailContractStatusColor = "#22C55E";
    [ObservableProperty] private string _detailStatusLabel = "—";
    [ObservableProperty] private string _detailPresenceLabel = "—";
    [ObservableProperty] private string _detailPresenceBadgeBackground = "#F1F5F9";
    [ObservableProperty] private string _detailPresenceBadgeForeground = "#64748B";
    [ObservableProperty] private int _detailSalaryPaymentsCount;

    [ObservableProperty] private string _formAddress = string.Empty;
    [ObservableProperty] private string _formGender = string.Empty;
    [ObservableProperty] private string _formNationalId = string.Empty;
    [ObservableProperty] private DateTime? _formBirthDate;
    [ObservableProperty] private string _formMaritalStatus = string.Empty;
    [ObservableProperty] private string _formEmergencyContactName = string.Empty;
    [ObservableProperty] private string _formEmergencyContactPhone = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formContractNumber = string.Empty;
    [ObservableProperty] private string _formContractType = "CDI";
    [ObservableProperty] private DateTime? _formContractStart;
    [ObservableProperty] private DateTime? _formContractEnd;
    [ObservableProperty] private string _formSupervisor = string.Empty;
    [ObservableProperty] private string _formWorkSchedule = "Lun–Ven 8h–17h";

    [ObservableProperty] private int _kpiColumns = 6;
    [ObservableProperty] private bool _isCompactHeader;
    [ObservableProperty] private bool _useCompactFilters;
    [ObservableProperty] private bool _showDetailSidebar;
    [ObservableProperty] private bool _showDetailStacked;
    [ObservableProperty] private bool _showDetailOverlay;
    [ObservableProperty] private GridLength _rightSidebarColumnWidth = new(280);
    [ObservableProperty] private Thickness _tableCardMargin = new(0, 0, 8, 0);

    [ObservableProperty] private ISeries[] _payrollTrendSeries = [];
    [ObservableProperty] private ISeries[] _departmentPieSeries = [];
    [ObservableProperty] private ISeries[] _presenceBarSeries = [];

    [ObservableProperty] private bool _showColumnMatricule = true;
    [ObservableProperty] private bool _showColumnPhone = true;
    [ObservableProperty] private bool _showColumnHireDate = true;
    [ObservableProperty] private bool _showColumnPresence = true;
    [ObservableProperty] private bool _showColumnDepartment = true;
    [ObservableProperty] private bool _showCompactEmployeeCell;
    [ObservableProperty] private bool _useHorizontalTableScroll;

    private double _lastViewWidth = 1400;

    public ObservableCollection<PersonnelAlertItem> Alerts { get; } = [];
    public ObservableCollection<PersonnelSummaryLine> PresenceSummaryLines { get; } = [];
    public ObservableCollection<PersonnelDepartmentSlice> DepartmentSlices { get; } = [];
    public ObservableCollection<PersonnelEmployeeItem> Employees { get; } = [];
    public ObservableCollection<string> Departments { get; } = [AllDepartments];
    public ObservableCollection<string> Positions { get; } = [AllPositions];
    public ObservableCollection<string> Statuses { get; } =
        [AllStatuses, "Actif", "Suspendu", "Congé", "Renvoyé", "En attente", "Inactif"];
    public ObservableCollection<string> Presences { get; } =
        [AllPresences, "Présent", "Absent", "En congé", "Retard", "Sortie anticipée", "Non pointé", "Inactif"];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<PersonnelContractRow> DetailContracts { get; } = [];
    public ObservableCollection<PersonnelSalaryRow> DetailSalaryPayments { get; } = [];
    public ObservableCollection<PersonnelAttendanceRow> DetailAttendances { get; } = [];
    public ObservableCollection<PersonnelActivityRow> DetailActivities { get; } = [];
    public ObservableCollection<string> ContractTypes { get; } = ["CDI", "CDD", "Stage", "Intérim", "Consultant"];

    public PersonnelViewModel(
        PersonnelService personnelService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _personnelService = personnelService;
        _syncService = syncService;
        appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        NotificationCount = 5;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        CanManagePersonnel = _session.HasPermission(PermissionCodes.PersonnelManage);
        try
        {
            var data = await _personnelService.LoadAsync();
            _allEmployees = data.Employees.ToList();

            TotalEmployees = data.TotalEmployees;
            PresentToday = data.PresentToday;
            AbsentToday = data.AbsentToday;
            OnLeaveToday = data.OnLeaveToday;
            LateToday = data.LateToday;
            NewThisMonth = data.NewThisMonth;
            MonthlyPayroll = MoneyFormatter.Format(data.MonthlyPayroll);
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            RentCollectedTotalDisplay = $"Ce mois : {MoneyFormatter.Format(data.RentCollectedThisMonth)}";

            var t = Math.Max(TotalEmployees, 1);
            PresentPercent = $"{PresentToday * 100.0 / t:F2}%";
            AbsentPercent = $"{AbsentToday * 100.0 / t:F2}%";
            OnLeavePercent = $"{OnLeaveToday * 100.0 / t:F2}%";
            PresenceRateDisplay = $"{data.PresenceRate:F1}%";

            Alerts.Clear();
            foreach (var alert in data.Alerts)
                Alerts.Add(alert);
            NotificationCount = data.Alerts.Count(a => a.Color is "#F97316" or "#EAB308" or "#8B5CF6");

            DepartmentSlices.Clear();
            foreach (var slice in data.Departments)
                DepartmentSlices.Add(slice);

            PresenceSummaryLines.Clear();
            PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Taux de présence", ValueDisplay = PresenceRateDisplay, Color = "#2D6A4F", IsBold = true });
            PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Présents", ValueDisplay = PresentToday.ToString(), Color = "#166534" });
            PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Absents", ValueDisplay = AbsentToday.ToString(), Color = "#DC2626" });
            PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "En congé", ValueDisplay = OnLeaveToday.ToString(), Color = "#7C3AED" });
            PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Retards", ValueDisplay = LateToday.ToString(), Color = "#EAB308" });

            BuildCharts(data);

            Departments.Clear();
            Departments.Add(AllDepartments);
            foreach (var d in _allEmployees.Select(e => e.Department).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
                Departments.Add(d);

            Positions.Clear();
            Positions.Add(AllPositions);
            foreach (var p in _allEmployees.Select(e => e.Position).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
                Positions.Add(p);

            CurrentPage = 1;
            ApplyFilters();
            if (SelectedEmployee is null || !_allEmployees.Any(e => e.Id == SelectedEmployee.Id))
                SelectedEmployee = Employees.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectEmployee(PersonnelEmployeeItem? employee)
    {
        if (employee is not null)
        {
            SelectedEmployee = employee;
            SelectedDetailTab = 0;
        }
    }

    [RelayCommand]
    private async Task ViewEmployeeAsync(PersonnelEmployeeItem? employee)
    {
        if (employee is null)
            return;
        await OpenEmployeeDetailPageAsync(employee);
    }

    [RelayCommand]
    private async Task EditEmployeeAsync(PersonnelEmployeeItem? employee)
    {
        if (employee is null)
            return;
        await OpenEmployeeDetailPageAsync(employee);
    }

    [RelayCommand]
    private void GoBackFromEmployeeDetail()
    {
        IsEmployeeDetailPageOpen = false;
        IsEmployeeDetailEditMode = false;
        IsEditingEmployee = false;
        FormError = null;
        ApplyLayoutMetrics(_lastViewWidth);
    }

    public async Task OpenEmployeeDetailPageAsync(PersonnelEmployeeItem item)
    {
        ErrorMessage = null;
        SelectedEmployee = item;
        IsDetailPanelOpen = false;
        SelectedDetailTab = 0;
        IsEmployeeDetailEditMode = false;
        IsEmployeeDetailPageOpen = true;
        ApplyLayoutMetrics(_lastViewWidth);

        if (!await LoadEmployeeDetailAsync(item.Id))
        {
            IsEmployeeDetailPageOpen = false;
            ErrorMessage = "Impossible de charger la fiche employé.";
        }
    }

    [RelayCommand]
    private void EnterEmployeeDetailEditMode()
    {
        IsEmployeeDetailEditMode = true;
        FormError = null;
    }

    [RelayCommand]
    private async Task CancelEmployeeDetailEditAsync()
    {
        IsEmployeeDetailEditMode = false;
        FormError = null;
        if (EditingEmployeeId != Guid.Empty)
            await LoadEmployeeDetailAsync(EditingEmployeeId);
    }

    [RelayCommand]
    private void OpenPointagePopup(PersonnelEmployeeItem? employee)
    {
        if (employee is null || !employee.StatusLabel.Equals("Actif", StringComparison.OrdinalIgnoreCase))
            return;
        PointageEmployee = employee;
        PointageLeaveReason = string.Empty;
        PointageError = null;
        IsPointagePopupOpen = true;
    }

    [RelayCommand]
    private void ClosePointagePopup()
    {
        IsPointagePopupOpen = false;
        PointageEmployee = null;
        PointageLeaveReason = string.Empty;
        PointageError = null;
    }

    [RelayCommand]
    private async Task SetPointageAsync(object? parameter)
    {
        var employee = PointageEmployee ?? SelectedEmployee;
        if (employee is null)
            return;

        var kind = parameter?.ToString() switch
        {
            "present" => PersonnelPointageKind.Present,
            "checkout" => PersonnelPointageKind.CheckOut,
            "absent" => PersonnelPointageKind.Absent,
            "leave" => PersonnelPointageKind.Leave,
            _ => (PersonnelPointageKind?)null
        };

        if (kind is null)
            return;

        if (kind == PersonnelPointageKind.Leave)
        {
            if (!IsPointagePopupOpen)
            {
                OpenPointagePopup(employee);
                return;
            }

            if (string.IsNullOrWhiteSpace(PointageLeaveReason))
            {
                PointageError = "Indiquez le motif du congé (maladie, personnel, etc.).";
                return;
            }
        }

        IsBusy = true;
        PointageError = null;
        try
        {
            var updated = await _personnelService.RecordPointageAsync(
                employee.Id,
                kind.Value,
                kind == PersonnelPointageKind.Leave ? PointageLeaveReason : null);

            if (updated is null)
            {
                PointageError = "Impossible d'enregistrer le pointage.";
                return;
            }

            ReplaceEmployeeInCache(updated);
            IsPointagePopupOpen = false;
            PointageEmployee = null;
            PointageLeaveReason = string.Empty;
            StatusMessage = "Pointage enregistré.";
            await RefreshKpisAsync();
            if (IsEmployeeDetailPageOpen && EditingEmployeeId != Guid.Empty)
                await LoadEmployeeDetailAsync(EditingEmployeeId);
        }
        catch (Exception ex)
        {
            PointageError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadEmployeeDetailAsync(Guid employeeId)
    {
        var detail = await _personnelService.GetEmployeeDetailAsync(employeeId);
        if (detail is null)
            return false;

        ApplyEmployeeDetail(detail);
        await LoadEmployeeIntoFormAsync(employeeId);
        await LoadDetailAttendancesAsync();
        return true;
    }

    private void ApplyEmployeeDetail(PersonnelEmployeeDetailData d)
    {
        EmployeeDetailSubtitle = d.SummaryLine;
        DetailInitials = d.Initials;
        DetailSummaryLine = d.SummaryLine;
        DetailPhone = d.Phone;
        DetailEmail = d.Email;
        DetailAddress = d.Address;
        DetailGender = d.Gender;
        DetailBirthDateDisplay = d.DateOfBirthDisplay;
        DetailAgeDisplay = d.AgeDisplay;
        DetailNationalId = d.NationalId;
        DetailMaritalStatus = d.MaritalStatus;
        DetailEmergencyName = d.EmergencyContactName;
        DetailEmergencyPhone = d.EmergencyContactPhone;
        DetailNotes = d.Notes;
        DetailHireDateDisplay = d.HireDateDisplay;
        DetailSupervisor = d.Supervisor;
        DetailWorkSchedule = d.WorkSchedule;
        DetailSalaryDisplay = d.BaseSalaryDisplay;
        DetailContractNumber = d.ContractNumber;
        DetailContractType = d.ContractType;
        DetailContractStartDisplay = d.ContractStartDisplay;
        DetailContractEndDisplay = d.ContractEndDisplay;
        DetailContractStatus = d.ContractStatusLabel;
        DetailContractStatusColor = d.ContractStatusColor;
        DetailStatusLabel = d.StatusLabel;
        DetailPresenceLabel = d.PresenceLabel;
        DetailPresenceBadgeBackground = d.PresenceBadgeBackground;
        DetailPresenceBadgeForeground = d.PresenceBadgeForeground;
        DetailSalaryPaymentsCount = d.SalaryPaymentsCount;

        DetailContracts.Clear();
        foreach (var c in d.Contracts) DetailContracts.Add(c);
        DetailSalaryPayments.Clear();
        foreach (var s in d.SalaryPayments) DetailSalaryPayments.Add(s);
        DetailActivities.Clear();
        foreach (var a in d.Activities) DetailActivities.Add(a);
        DetailDisciplinaryNotes.Clear();
        foreach (var n in d.DisciplinaryNotes) DetailDisciplinaryNotes.Add(n);
        DetailSeniorityDisplay = d.SeniorityDisplay;
        DetailContractPdfPath = d.ContractPdfPath ?? string.Empty;
        ApplyProfilePhotoFromDetail(d.ProfilePhotoPath);
        DetailPresenceStatsLine = $"Ce mois : {d.PresenceStats.PresentDays} présences, {d.PresenceStats.LateDays} retards, {d.PresenceStats.TotalWorkedHours:N1} h travaillées";

        OnPropertyChanged(nameof(EmployeeDetailTitle));
    }

    private async Task<bool> LoadEmployeeIntoFormAsync(Guid employeeId)
    {
        var employee = await _personnelService.GetEmployeeAsync(employeeId);
        if (employee is null)
        {
            ErrorMessage = "Employé introuvable.";
            return false;
        }

        PopulateFormFromEmployee(employee);
        return true;
    }

    private async Task<bool> LoadEmployeeIntoFormAsync(PersonnelEmployeeItem item) =>
        await LoadEmployeeIntoFormAsync(item.Id);

    private void PopulateFormFromEmployee(Employee employee)
    {
        FormError = null;
        IsEditingEmployee = true;
        EditingEmployeeId = employee.Id;
        FormMatricule = employee.Matricule;
        FormFirstName = employee.FirstName;
        FormLastName = employee.LastName;
        FormPosition = employee.Position;
        FormDepartment = employee.Department;
        FormPhone = employee.Phone;
        FormEmail = employee.Email;
        FormBaseSalaryText = employee.BaseSalary.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        FormHireDate = employee.HireDate;
        FormIsActive = employee.IsActive;
        FormRhStatus = string.IsNullOrWhiteSpace(employee.RhStatus)
            ? RhConstants.EmployeeStatus.Active
            : employee.RhStatus;
        FormAddress = employee.Address;
        FormGender = employee.Gender;
        FormNationalId = employee.NationalId;
        FormBirthDate = employee.BirthDate;
        FormMaritalStatus = employee.MaritalStatus;
        FormEmergencyContactName = employee.EmergencyContactName;
        FormEmergencyContactPhone = employee.EmergencyContactPhone;
        FormNotes = employee.Notes;
        FormContractNumber = string.IsNullOrWhiteSpace(employee.ContractNumber)
            ? $"CTR-{employee.Matricule}"
            : employee.ContractNumber;
        FormContractType = string.IsNullOrWhiteSpace(employee.ContractType) ? "CDI" : employee.ContractType;
        FormContractStart = employee.ContractStartDate ?? employee.HireDate;
        FormContractEnd = employee.ContractEndDate;
        FormSupervisor = employee.Supervisor;
        FormWorkSchedule = string.IsNullOrWhiteSpace(employee.WorkSchedule) ? "Lun–Ven 8h–17h" : employee.WorkSchedule;
        OnPropertyChanged(nameof(EmployeeDetailTitle));
    }

    private Employee BuildEmployeeFromForm() => new()
    {
        Id = EditingEmployeeId,
        Matricule = FormMatricule,
        FirstName = FormFirstName,
        LastName = FormLastName,
        Position = FormPosition,
        Department = FormDepartment,
        Phone = FormPhone,
        Email = FormEmail,
        BaseSalary = decimal.TryParse(FormBaseSalaryText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var salary) ? salary : 0,
        HireDate = FormHireDate,
        IsActive = FormIsActive,
        Address = FormAddress,
        Gender = FormGender,
        NationalId = FormNationalId,
        BirthDate = FormBirthDate,
        MaritalStatus = FormMaritalStatus,
        EmergencyContactName = FormEmergencyContactName,
        EmergencyContactPhone = FormEmergencyContactPhone,
        Notes = FormNotes,
        ContractNumber = FormContractNumber,
        ContractType = FormContractType,
        ContractStartDate = FormContractStart ?? FormHireDate,
        ContractEndDate = FormContractEnd,
        Supervisor = FormSupervisor,
        WorkSchedule = FormWorkSchedule,
        RhStatus = FormRhStatus
    };

    [RelayCommand]
    private void CancelEmployeeEditor()
    {
        IsEmployeeEditorOpen = false;
        IsAddFormOpen = false;
        IsEditingEmployee = false;
        FormError = null;
    }

    partial void OnIsEditingEmployeeChanged(bool value)
    {
        OnPropertyChanged(nameof(EmployeeFormTitle));
        OnPropertyChanged(nameof(EmployeeFormSubtitle));
    }

    [RelayCommand]
    private async Task SaveEmployeeEditorAsync()
    {
        FormError = null;
        IsBusy = true;
        try
        {
            string error;
            var entity = BuildEmployeeFromForm();
            if (IsEditingEmployee)
                error = await _personnelService.UpdateEmployeeAsync(entity);
            else
                error = await _personnelService.CreateEmployeeAsync(entity);

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            var wasEditing = IsEditingEmployee;
            var savedId = EditingEmployeeId;
            var keepDetailOpen = IsEmployeeDetailPageOpen && wasEditing;
            IsEmployeeEditorOpen = false;
            IsAddFormOpen = false;
            IsEmployeeDetailEditMode = false;
            if (!keepDetailOpen)
                IsEmployeeDetailPageOpen = false;
            IsEditingEmployee = false;
            StatusMessage = wasEditing ? "Employé mis à jour." : "Employé enregistré avec succès.";
            await LoadAsync();
            if (keepDetailOpen && savedId != Guid.Empty)
                await LoadEmployeeDetailAsync(savedId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceEmployeeInCache(PersonnelEmployeeItem updated)
    {
        var index = _allEmployees.FindIndex(e => e.Id == updated.Id);
        if (index >= 0)
            _allEmployees[index] = updated;

        for (var i = 0; i < Employees.Count; i++)
        {
            if (Employees[i].Id == updated.Id)
            {
                Employees[i] = updated;
                break;
            }
        }

        if (SelectedEmployee?.Id == updated.Id)
            SelectedEmployee = updated;

        ApplyFilters();
    }

    private async Task RefreshKpisAsync()
    {
        var data = await _personnelService.LoadAsync();
        _allEmployees = data.Employees.ToList();
        TotalEmployees = data.TotalEmployees;
        PresentToday = data.PresentToday;
        AbsentToday = data.AbsentToday;
        OnLeaveToday = data.OnLeaveToday;
        LateToday = data.LateToday;
        var t = Math.Max(TotalEmployees, 1);
        PresentPercent = $"{PresentToday * 100.0 / t:F2}%";
        AbsentPercent = $"{AbsentToday * 100.0 / t:F2}%";
        OnLeavePercent = $"{OnLeaveToday * 100.0 / t:F2}%";
        PresenceRateDisplay = $"{data.PresenceRate:F1}%";
        MonthlyPayroll = MoneyFormatter.Format(data.MonthlyPayroll);
        AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
        RentCollectedTotalDisplay = $"Ce mois : {MoneyFormatter.Format(data.RentCollectedThisMonth)}";
        Alerts.Clear();
        foreach (var alert in data.Alerts)
            Alerts.Add(alert);
        NotificationCount = data.Alerts.Count(a => a.Color is "#F97316" or "#EAB308" or "#8B5CF6");
        DepartmentSlices.Clear();
        foreach (var slice in data.Departments)
            DepartmentSlices.Add(slice);
        PresenceSummaryLines.Clear();
        PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Taux de présence", ValueDisplay = PresenceRateDisplay, Color = "#2D6A4F", IsBold = true });
        PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Présents", ValueDisplay = PresentToday.ToString(), Color = "#166534" });
        PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Absents", ValueDisplay = AbsentToday.ToString(), Color = "#DC2626" });
        PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "En congé", ValueDisplay = OnLeaveToday.ToString(), Color = "#7C3AED" });
        PresenceSummaryLines.Add(new PersonnelSummaryLine { Label = "Retards", ValueDisplay = LateToday.ToString(), Color = "#EAB308" });
        BuildCharts(data);
        ApplyFilters();
    }

    [RelayCommand]
    private void CloseDetailPanel()
    {
        SelectedEmployee = null;
        IsDetailPanelOpen = false;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            await _syncService.SyncAsync(manual: true);
            await LoadAsync();
            StatusMessage = "Synchronisation terminée";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddEmployeeAsync()
    {
        FormError = null;
        FormMatricule = await _personnelService.GenerateNextMatriculeAsync();
        FormFirstName = string.Empty;
        FormLastName = string.Empty;
        FormPosition = string.Empty;
        FormDepartment = string.Empty;
        FormPhone = string.Empty;
        FormEmail = string.Empty;
        FormBaseSalaryText = "0";
        FormHireDate = DateTime.Today;
        FormIsActive = true;
        IsAddFormOpen = true;
    }

    [RelayCommand]
    private void CancelAddForm()
    {
        IsAddFormOpen = false;
        FormError = null;
    }

    [RelayCommand]
    private async Task SaveEmployeeAsync() => await SaveEmployeeEditorAsync();

    partial void OnSelectedEmployeeChanged(PersonnelEmployeeItem? value)
    {
        if (!IsEmployeeDetailPageOpen)
            IsDetailPanelOpen = value is not null;
    }

    partial void OnIsDetailPanelOpenChanged(bool value) =>
        ApplyLayoutMetrics(_lastViewWidth);

    partial void OnIsEmployeeDetailPageOpenChanged(bool value) =>
        ApplyLayoutMetrics(_lastViewWidth);

    partial void OnIsAddFormOpenChanged(bool value) =>
        OnPropertyChanged(nameof(IsEmployeeFormVisible));

    partial void OnIsEmployeeEditorOpenChanged(bool value) =>
        OnPropertyChanged(nameof(IsEmployeeFormVisible));

    public bool IsEmployeeFormVisible => IsAddFormOpen;

    public string EmployeeDetailTitle =>
        string.IsNullOrWhiteSpace(FormFirstName) && string.IsNullOrWhiteSpace(FormLastName)
            ? "Fiche employé"
            : $"{FormFirstName} {FormLastName}".Trim();

    /// <summary>Libellé du pointage journalier affiché au-dessus du tableau.</summary>
    public string PresenceDayCaption =>
        $"Présence journalière — {DateTime.Today.ToString("dddd dd MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))}";

    public string EmployeeFormTitle => IsEditingEmployee ? "Fiche employé" : "Nouvel employé";

    public string EmployeeFormSubtitle => IsEditingEmployee
        ? "Consultez et modifiez toutes les informations"
        : "Renseignez les informations pour créer un employé";

    public void UpdateViewWidth(double width)
    {
        _lastViewWidth = width;
        ApplyLayoutMetrics(width);
    }

    private void ApplyLayoutMetrics(double width)
    {
        var m = ResponsiveLayoutMetrics.FromWidth(width, IsDetailPanelOpen && !IsEmployeeDetailPageOpen);
        KpiColumns = m.KpiColumns;
        IsCompactHeader = m.IsCompactHeader;
        UseCompactFilters = m.UseCompactFilters;
        ShowDetailSidebar = m.ShowDetailSidebar;
        ShowDetailStacked = m.ShowDetailStacked;
        ShowDetailOverlay = m.ShowDetailOverlay;
        RightSidebarColumnWidth = width >= 1180 ? new GridLength(280) : new GridLength(0);
        TableCardMargin = width >= 1180 ? new Thickness(0, 0, 8, 0) : new Thickness(0);
        ShowColumnMatricule = m.ShowColumnMatricule;
        ShowColumnPhone = m.ShowColumnPhone;
        ShowColumnHireDate = m.ShowColumnHireDate;
        ShowColumnPresence = m.ShowColumnPresence;
        ShowColumnDepartment = m.ShowColumnDepartment;
        ShowCompactEmployeeCell = m.ShowCompactEmployeeCell;
        UseHorizontalTableScroll = m.UseHorizontalTableScroll;
    }

    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterDepartmentChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPositionChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPresenceChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter()
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();
        var filtered = _allEmployees.Where(e =>
            (FilterDepartment == AllDepartments || e.Department == FilterDepartment) &&
            (FilterPosition == AllPositions || e.Position == FilterPosition) &&
            (FilterStatus == AllStatuses || e.StatusLabel == FilterStatus) &&
            (FilterPresence == AllPresences || e.PresenceLabel == FilterPresence) &&
            (string.IsNullOrWhiteSpace(query) ||
             e.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Matricule.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Position.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Department.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = list.Skip(skip).Take(PageSize).ToList();

        Employees.Clear();
        foreach (var e in page) Employees.Add(e);

        var start = list.Count == 0 ? 0 : skip + 1;
        var end = skip + page.Count;
        PaginationText = $"Affichage de {start} à {end} sur {list.Count} employés";
    }

    private void BuildCharts(PersonnelPageData data)
    {
        var palette = new[] { "#2D6A4F", "#40916C", "#52B788", "#2563EB", "#EA580C", "#8B5CF6", "#64748B" };

        PayrollTrendSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Masse salariale",
                Values = data.PayrollTrend.ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2D6A4F")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F").WithAlpha(40)),
                GeometrySize = 6
            }
        ];

        DepartmentPieSeries = data.Departments.Select((s, i) => new PieSeries<int>
        {
            Name = s.Department,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        PresenceBarSeries =
        [
            new ColumnSeries<int>
            {
                Name = "Présents",
                Values = [data.PresentToday],
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            },
            new ColumnSeries<int>
            {
                Name = "Absents",
                Values = [data.AbsentToday],
                Fill = new SolidColorPaint(SKColor.Parse("#DC2626"))
            },
            new ColumnSeries<int>
            {
                Name = "Congés",
                Values = [data.OnLeaveToday],
                Fill = new SolidColorPaint(SKColor.Parse("#8B5CF6"))
            }
        ];
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpper() : name.Length >= 2 ? name[..2].ToUpper() : "AP";
    }
}
