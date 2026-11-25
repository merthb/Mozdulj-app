using Mozdulj.Core;
using Mozdulj.Maui.Services;
using System.Text.Json;

namespace Mozdulj.Maui.Pages;

/// <summary>Full-screen modal editor, owned by a NavigationPage. Built-ins never enter this page.</summary>
public sealed class ExerciseEditorPage : ContentPage
{
    private readonly AppState _state;
    private readonly Exercise _draft;
    private readonly Entry _name = new() { Placeholder = "Gyakorlat neve" };
    private readonly Picker _category = new() { Title = AppLocalization.Text("WorkoutUi002") };
    private readonly List<string> _areas = [];
    private readonly List<string> _details = [];
    private readonly List<string> _legacyDetails = [];
    private readonly VerticalStackLayout _detailChoices = new() { Spacing = 6 };
    private readonly Label _areaSummary = UI.Label("", 13, color:UI.Muted);
    private readonly Picker _phase = new() { Title = AppLocalization.Text("WorkoutUi055") };
    private readonly Picker _difficulty = new() { Title = AppLocalization.Text("Difficulty") };
    private readonly Picker _measure = new() { Title = AppLocalization.Text("WorkoutUi056") };
    private readonly Entry _seconds = new() { Placeholder = AppLocalization.Text("WorkoutUi057"), Keyboard = Keyboard.Numeric };
    private readonly Entry _reps = new() { Placeholder = AppLocalization.Text("WorkoutUi058") };
    private readonly Entry _estimate = new() { Placeholder = AppLocalization.Text("WorkoutUi059"), Keyboard = Keyboard.Numeric };
    private readonly Entry _equipment = new() { Placeholder = AppLocalization.Text("WorkoutUi060") };
    private readonly Editor _summary = new() { Placeholder = "Rövid végrehajtási leírás", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _position = new() { Placeholder = "Kiinduló helyzet", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _steps = new() { Placeholder = "Végrehajtási lépések (soronként)", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _mistakes = new() { Placeholder = "Gyakori hibák (soronként)", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _safety = new() { Placeholder = "Óvintézkedések, abbahagyás feltételei", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Entry _englishName = new() { Placeholder = "Exercise name" };
    private readonly Editor _englishSummary = new() { AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _englishPosition = new() { AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _englishSteps = new() { AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _englishMistakes = new() { AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _englishSafety = new() { AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly VerticalStackLayout _content = UI.Stack();
    private readonly Label _figureInfo = UI.Label("",13,color:UI.Muted);
    private readonly bool _open3dImmediately;
    private bool _opened3d;

    public ExerciseEditorPage(AppState state, Exercise? current = null, bool open3dImmediately=false)
    {
        _state = state; _open3dImmediately=open3dImmediately;
        _draft = current is null ? new Exercise
        {
            Id = "user-" + Guid.NewGuid().ToString("N"),
            Phase = "main", Measure = "reps", EstimatedSeconds = 45,
            Difficulty = 1, Pose = new MotionPose()
        } : ExerciseLibraryRules.Clone(current);
        ExerciseLibraryRules.Normalize(_draft);
        if (state.BuiltInCatalog.Exercises.Any(x=>x.Id==_draft.Id))
            throw new InvalidOperationException(AppLocalization.Text("WorkoutUi061"));
        UI.Setup(this,current is null ? AppLocalization.Text("WorkoutUi062") : AppLocalization.Text("WorkoutUi063"));
        foreach (var c in ExerciseLibraryRules.Categories) _category.Items.Add(PresentationText.Known(c));
        foreach (var p in new[] { AppLocalization.Text("WorkoutUi064"), AppLocalization.Text("WorkoutUi065"), AppLocalization.Text("WorkoutUi066") }) _phase.Items.Add(p);
        foreach (var d in new[] { AppLocalization.Text("Beginner"), AppLocalization.Text("Intermediate"), AppLocalization.Text("Advanced") }) _difficulty.Items.Add(d);
        _measure.Items.Add(AppLocalization.Text("WorkoutUi067")); _measure.Items.Add(AppLocalization.Text("WorkoutUi068"));
        _name.Text = _draft.Name;
        _category.SelectedIndex = Array.IndexOf(ExerciseLibraryRules.Categories,_draft.Category);
        _details.AddRange(_draft.Regions.Select(RegionTaxonomy.FindDetail).OfType<RegionDetail>().Select(d=>d.Key).Distinct());
        _legacyDetails.AddRange(_draft.Regions.Where(r=>!RegionTaxonomy.IsDetail(r)));
        _areas.AddRange(RegionTaxonomy.AreaIds(_details));
        _phase.SelectedIndex = Array.IndexOf(ExerciseLibraryRules.Phases,_draft.Phase);
        _difficulty.SelectedIndex = Math.Clamp(_draft.Difficulty-1,0,2);
        _measure.SelectedIndex = _draft.Measure=="time"?1:0;
        _seconds.Text = _draft.TargetSeconds?.ToString() ?? "";
        _reps.Text = string.Join("; ",_draft.RepTargets.Select(r=>$"{r.Count} {r.Label}"));
        _estimate.Text = _draft.EstimatedSeconds.ToString();
        _equipment.Text = string.Join(", ",_draft.Equipment);
        _summary.Text = _draft.Summary; _position.Text = _draft.StartingPosition;
        _steps.Text = string.Join("\n",_draft.Steps); _mistakes.Text=string.Join("\n",_draft.CommonMistakes);
        _safety.Text = _draft.SafetyNote;
        if (_draft.EnglishText is { } en)
        {
            _englishName.Text = en.Name;
            _englishSummary.Text = en.Summary;
            _englishPosition.Text = en.StartingPosition;
            _englishSteps.Text = string.Join("\n", en.Steps ?? []);
            _englishMistakes.Text = string.Join("\n", en.CommonMistakes ?? []);
            _englishSafety.Text = en.SafetyNote;
        }
        Fill();
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_open3dImmediately || _opened3d) return;
        _opened3d=true;
        Dispatcher.Dispatch(async () => await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose, pose =>
        {
            _draft.Pose=pose;
            UpdateFigureInfo();
        })));
    }
    private static List<string> Csv(string? text) => (text??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> Lines(string? text) => (text??"").Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
    private static View Field(string label,View input) => new VerticalStackLayout { Spacing=4,
        Children = { UI.Label(label,13,true,color:UI.Muted),input } };
    private void Fill()
    {
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi069"),26,true));
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi070"),13,color:UI.Muted));
        var language = new Picker { Title = AppLocalization.IsEnglish ? "Exercise text language" : "Gyakorlat szövegének nyelve" };
        language.Items.Add("Magyar"); language.Items.Add("English");
        _content.Children.Add(Field(AppLocalization.IsEnglish ? "Editing language" : "Szerkesztés nyelve",language));
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ?
            "Switch languages to edit the other text. Both versions are kept when you switch and saved together." :
            "Nyelvváltással a másik nyelvű szöveget szerkesztheted. Mindkét változat megmarad váltáskor, és együtt menthető.",13,color:UI.Muted));
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ? "Basic information" : "Alapadatok",20,true));
        var huNameField = Field("Gyakorlat neve (HU)",_name);
        var enNameField = Field("Exercise name (EN)",_englishName);
        _content.Children.Add(huNameField);
        _content.Children.Add(enNameField);
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi071"),_category));
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ? "Affected body areas *" : "Érintett testtájak *", 13, true));
        foreach (var group in new[] { "upper", "lower", "full" })
        {
            _content.Children.Add(UI.Label(RegionTaxonomy.GroupLabel(group), 13, true));
            _content.Children.Add(ChoiceChips.Create(RegionTaxonomy.Areas.Where(a=>a.Group==group).Select(a=>a.Id),
                _areas, label: RegionTaxonomy.AreaLabel, changed: RefreshDetails));
        }
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ? "Detailed body parts *" : "Részletes testrészek *", 13, true));
        _content.Children.Add(_detailChoices);
        _content.Children.Add(_areaSummary);
        if (_legacyDetails.Count > 0)
            _content.Children.Add(UI.Label((AppLocalization.IsEnglish ? "Legacy parts — choose replacements before saving: " :
                "Korábbi egyedi testtájak – mentés előtt válassz megfelelő elemeket: ") + string.Join(", ", _legacyDetails), 13, color:UI.Muted));
        RefreshDetails();
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi072"),_phase));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi073"),_difficulty));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi074"),_measure));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi075"),_seconds));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi076"),_reps));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi077"),_estimate));
        _content.Children.Add(Field(AppLocalization.Text("WorkoutUi078"),_equipment));
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ? "Description and safety" : "Leírás és biztonság",20,true));
        _content.Children.Add(UI.Label(AppLocalization.IsEnglish ?
            "Provide a name and instructions in at least one language. Missing text uses the other language." :
            "Legalább egy nyelven adj meg nevet és végrehajtási leírást. A hiányzó szöveg helyén a másik nyelv jelenik meg.",13,color:UI.Muted));
        var huFields = new VerticalStackLayout { Spacing=8, Children = {
            Field("Rövid leírás",_summary), Field("Kiinduló helyzet",_position),
            Field("Végrehajtási lépések (soronként)",_steps), Field("Gyakori hibák",_mistakes), Field("Biztonsági megjegyzés",_safety) }};
        var enFields = new VerticalStackLayout { Spacing=8, Children = {
            Field("Summary",_englishSummary), Field("Starting position",_englishPosition),
            Field("Steps (one per line)",_englishSteps), Field("Common mistakes",_englishMistakes), Field("Safety note",_englishSafety) }};
        void ShowLanguage()
        {
            bool english = language.SelectedIndex == 1;
            huNameField.IsVisible = huFields.IsVisible = !english;
            enNameField.IsVisible = enFields.IsVisible = english;
        }
        language.SelectedIndex = AppLocalization.IsEnglish ? 1 : 0;
        language.SelectedIndexChanged += (_,_)=>ShowLanguage();
        ShowLanguage();
        _content.Children.Add(huFields);
        _content.Children.Add(enFields);
        _content.Children.Add(UI.Label(AppLocalization.Text("WorkoutUi079"),20,true));
        _content.Children.Add(_figureInfo);
        var draw=UI.Button(AppLocalization.Text("WorkoutUi080"),false);
        draw.Clicked+=async (_,_)=>{
            await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose, pose=>
            { _draft.Pose=pose; UpdateFigureInfo(); }));
        };
        _content.Children.Add(draw);
        var saveDraft=UI.Button(AppLocalization.Text("WorkoutUi081"),false);
        saveDraft.Clicked+=async (_,_)=> await Save(LibraryStatuses.Draft);
        var activate=UI.Button(AppLocalization.Text("WorkoutUi082"));
        activate.Clicked+=async (_,_)=> await Save(LibraryStatuses.Active);
        var cancel=UI.Button(AppLocalization.Text("Cancel"),false);
        cancel.Clicked+=async (_,_)=> await Navigation.PopModalAsync();
        var buttons=new VerticalStackLayout {Spacing=6,Children={activate,saveDraft,cancel}};
        var layout=new Grid {RowDefinitions={new RowDefinition{Height=GridLength.Star},new RowDefinition{Height=GridLength.Auto}}};
        var scroll=new ScrollView{Content=_content};layout.Add(scroll,0,0);
        var footer=new Border {BackgroundColor=UI.Background,Padding=new Thickness(18,8,18,16),Content=buttons};
        layout.Add(footer,0,1);Content=layout;UpdateFigureInfo();
    }
    private void UpdateFigureInfo() => _figureInfo.Text=_draft.Pose.Rig3D is null
        ? AppLocalization.Text("WorkoutUi083")
        : AppLocalization.Format("WorkoutUi262", _draft.Pose.Rig3D.Frames.Count, _draft.Pose.Rig3D.Preset);

    private void RefreshDetails()
    {
        var allowed = _areas.SelectMany(RegionTaxonomy.DetailsForArea).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _details.RemoveAll(d => !allowed.Contains(d));
        _detailChoices.Children.Clear();
        // Render a shared detail once even if it belongs to several selected areas.
        foreach (var area in RegionTaxonomy.Areas.Where(a => _areas.Contains(a.Id)))
        {
            var earlier = RegionTaxonomy.Areas.TakeWhile(a=>a.Id!=area.Id).Where(a=>_areas.Contains(a.Id))
                .SelectMany(a=>a.Details).Select(d=>d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keys = area.Details.Select(d=>d.Key).Where(d=>!earlier.Contains(d)).ToList();
            if (keys.Count == 0) continue;
            _detailChoices.Children.Add(UI.Label(RegionTaxonomy.AreaLabel(area.Id), 13, true));
            _detailChoices.Children.Add(ChoiceChips.Create(keys, _details, label: RegionTaxonomy.DetailLabel, changed: UpdateAreaSummary));
        }
        UpdateAreaSummary();
    }

    private void UpdateAreaSummary()
    {
        _areaSummary.Text = (AppLocalization.IsEnglish ? "Derived areas: " : "Testrészekhez rendelt testtájak: ") +
            RegionTaxonomy.AreaLabels(_details);
    }

    private void Gather()
    {
        _draft.Name=_name.Text?.Trim()??"";
        _draft.Category=_category.SelectedIndex>=0 ? ExerciseLibraryRules.Categories[_category.SelectedIndex] : "";
        _draft.Regions = _details.ToList();
        _draft.RegionGroups = new(StringComparer.OrdinalIgnoreCase);
        _draft.Phase=(_phase.SelectedIndex>=0 && _phase.SelectedIndex<3)?ExerciseLibraryRules.Phases[_phase.SelectedIndex]:"";
        _draft.Difficulty=_difficulty.SelectedIndex+1;
        _draft.Measure=_measure.SelectedIndex==1?"time":"reps";
        _draft.TargetSeconds = _draft.Measure=="time" && int.TryParse(_seconds.Text,out var sec)?sec:null;
        _draft.RepTargets=[];
        if(_draft.Measure=="reps")
        {
            foreach(var (piece,i) in (_reps.Text??"").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select((x,i)=>(x,i)))
            {
                var parts=piece.Split(' ',2,StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length>=1 && int.TryParse(parts[0],out var count))
                    _draft.RepTargets.Add(new RepTarget {Code=$"segment-{i}",Count=count,
                        Label=parts.Length>1?parts[1]:"ismétlés"});
            }
        }
        _draft.EstimatedSeconds=int.TryParse(_estimate.Text,out var estimated)?estimated:0;
        _draft.Equipment=Csv(_equipment.Text);
        _draft.Summary=_summary.Text?.Trim()??"";
        _draft.StartingPosition=_position.Text?.Trim()??"";
        _draft.Steps=Lines(_steps.Text);
        _draft.CommonMistakes=Lines(_mistakes.Text);
        _draft.SafetyNote=_safety.Text?.Trim()??"";
        var english = new ExerciseTranslation { Name=_englishName.Text?.Trim()??"", Summary=_englishSummary.Text?.Trim()??"",
            StartingPosition=_englishPosition.Text?.Trim()??"", Steps=Lines(_englishSteps.Text),
            CommonMistakes=Lines(_englishMistakes.Text), SafetyNote=_englishSafety.Text?.Trim()??"" };
        _draft.EnglishText = english.HasAny ? english : null;
    }
    private async Task Save(string status)
    {
        Gather();
        var derived = RegionTaxonomy.AreaIds(_details);
        if (_areas.Any(id => !derived.Contains(id)) || (_legacyDetails.Count > 0 && _details.Count == 0))
        {
            await DisplayAlertAsync(AppLocalization.IsEnglish ? "Choose detailed body parts" : "Válassz részletes testrészeket",
                AppLocalization.IsEnglish ? "Select at least one detailed part for every chosen area." :
                    "Minden kijelölt testtájhoz válassz legalább egy részletes testrészt.", AppLocalization.Text("OK"));
            return;
        }
        if(status==LibraryStatuses.Active)
        {
            var validation=ExerciseLibraryRules.ValidateForActivation(_draft);
            if(!validation.IsValid)
            {await DisplayAlertAsync(AppLocalization.Text("WorkoutUi084"),string.Join("\n",validation.Errors),AppLocalization.Text("OK"));return;}
        }
        try
        {
            await _state.SaveCustomExerciseAsync(_draft,status);
            await Navigation.PopModalAsync();
        }
        catch(Exception ex){await DisplayAlertAsync(AppLocalization.Text("SaveError"),ex.Message,AppLocalization.Text("OK"));}
    }
}
