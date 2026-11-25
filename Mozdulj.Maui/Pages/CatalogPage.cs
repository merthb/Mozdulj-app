using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Layouts;
using System.Collections.ObjectModel;
using Mozdulj.Core;
using Mozdulj.Maui.Services;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// Native virtualized library. Never put the CollectionView in a ScrollView or a
/// StackLayout with unbounded height: Android would measure/render every pose.
/// Filters scan lightweight metadata; cards and pose renderers are only created
/// for visible rows. RemainingItemsThreshold appends 12 more on demand.
/// </summary>
public sealed class CatalogPage : ContentPage
{
    private const int BatchSize = 12;
    private readonly AppState _state;
    private readonly SearchBar _search = new() { Placeholder = "Gyakorlat vagy testtáj…" };
    private readonly Picker _category = new() { Title = "Mozgástípus" };
    private readonly Picker _region = new() { Title = AppLocalization.IsEnglish ? "Body area" : "Testtáj" };
    private readonly Picker _detailRegion = new() { Title = AppLocalization.IsEnglish ? "Detailed body part" : "Részletes testrész" };
    private readonly Picker _origin = new() { Title = "Forrás / állapot" };
    private readonly Switch _favoritesOnly = new();
    private readonly Switch _availableOnly = new();
    private readonly ObservableCollection<LibraryRow> _visible = [];
    private readonly CollectionView _items;
    private readonly Label _counter = UI.Label("Gyakorlatok betöltése…", 13, color:UI.Muted);
    private List<LibraryRow> _matches = [];
    private List<LibraryRow> _librarySnapshot = [];
    private bool _refreshing;
    private bool _loading;
    private CancellationTokenSource? _searchDelay;
    private int _snapshotRevision = -1;
    private bool _snapshotReady;
    private bool _presented;
    private bool _isAppearing;
    private string _lastAppliedQuery = "";
    private HashSet<string> _lastFavorites = new(StringComparer.Ordinal);
    private HashSet<string> _lastEquipment = new(StringComparer.Ordinal);

    public CatalogPage(AppState state)
    {
        _state = state;
        UI.Setup(this, "Gyakorlattár");
        foreach (var option in new[] { "Minden gyakorlat", "Alapgyakorlatok", "Saját gyakorlatok", "Piszkozatok", "Kikapcsolt" })
            _origin.Items.Add(option);
        _origin.SelectedIndex = 0;
        _search.TextChanged += (_,_) => DebounceSearch();
        _category.SelectedIndexChanged += (_,_) => RefreshMatches();
        _region.SelectedIndexChanged += (_,_) => RefreshMatches();
        _detailRegion.SelectedIndexChanged += (_,_) => RefreshMatches();
        _origin.SelectedIndexChanged += (_,_) => RefreshMatches();
        _favoritesOnly.Toggled += (_,_) => RefreshMatches();
        _availableOnly.Toggled += (_,_) => RefreshMatches();

        _items = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsSource = _visible,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 },
            // Each item must be measured: a multi-line name or extra action changes row height.
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset,
            RemainingItemsThreshold = -1,
            EmptyView = UI.Label("Nincs találat. Próbálj másik keresőkifejezést, vagy módosítsd a szűrőket.", 14, color: UI.Muted),
            ItemTemplate = new DataTemplate(() =>
            {
                // DataTemplate is realized only for the visible native rows.
                var host = new ContentView { Padding = new Thickness(14, 0) };
                host.BindingContextChanged += (_,_) =>
                {
                    if (host.BindingContext is LibraryRow row)
                        host.Content = BuildCard(row);
                };
                return host;
            })
        };
        _items.RemainingItemsThresholdReached += (_,_) => LoadNextBatch();

        var header = new VerticalStackLayout { Padding = new Thickness(18,16,18,8), Spacing = 9 };
        header.Children.Add(UI.Label("Gyakorlattár", 25, true));
        // Keep the native editor mounted independently of the virtualized results.
        header.Children.Add(UI.Label("Keresés", 14, true));
        SemanticProperties.SetDescription(_search, "Gyakorlat vagy testtáj keresése");
        header.Children.Add(_search);

        var create = UI.Button("+ Új saját gyakorlat");
        create.Clicked += async (_,_) => await Navigation.PushModalAsync(new NavigationPage(new ExerciseEditorPage(_state))
            { BarBackgroundColor=UI.Background, BarTextColor=UI.Ink });
        var toggleFilters = UI.Button("⚙ További szűrők", false);
        toggleFilters.FontSize=13;
        create.FontSize=13;
        var actions = new Grid { ColumnSpacing = 8, ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star }
        }};
        actions.Children.Add(create);
        Grid.SetColumn(toggleFilters, 1);
        actions.Children.Add(toggleFilters);
        header.Children.Add(actions);
        var filterPanel = new VerticalStackLayout { Spacing=7 };
        // Source is a frequently-used filter: keep it visible without expanding a tall panel.
        var quickSources = new Grid { ColumnSpacing=5, ColumnDefinitions = {
            new ColumnDefinition { Width=GridLength.Star }, new ColumnDefinition { Width=GridLength.Star },
            new ColumnDefinition { Width=GridLength.Star } } };
        var sourceButtons = new [] { UI.Button("Mind",false),UI.Button("Alap",false),UI.Button("Saját",false) };
        for(int i=0;i<sourceButtons.Length;i++)
        {
            int idx=i;Grid.SetColumn(sourceButtons[i],i);quickSources.Children.Add(sourceButtons[i]);
            sourceButtons[i].Clicked += (_,_) => _origin.SelectedIndex=idx;
            sourceButtons[i].MinimumHeightRequest=44;
            sourceButtons[i].FontSize=13;
        }
        void SyncSourceButtons()
        {
            for(int i=0;i<3;i++)
            {
                var active=_origin.SelectedIndex==i;
                sourceButtons[i].BackgroundColor=active ? UI.PrimaryButton : UI.Lavender;
                sourceButtons[i].TextColor=active ? Colors.White : UI.Purple;
            }
        }
        _origin.SelectedIndexChanged+=(_,_)=>SyncSourceButtons();
        SyncSourceButtons();
        filterPanel.Children.Add(quickSources);
        var filterGrid = new Grid { ColumnSpacing=8,RowSpacing=3,
            ColumnDefinitions={new ColumnDefinition {Width=GridLength.Star},
                               new ColumnDefinition {Width=GridLength.Star}},
            RowDefinitions={new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto},
                            new RowDefinition {Height=GridLength.Auto}}};
        void Cell(View view,int column,int row) {Grid.SetColumn(view,column);Grid.SetRow(view,row);filterGrid.Children.Add(view);}
        Cell(UI.Label("Mozgástípus",12,true,color:UI.Muted),0,0);
        Cell(UI.Label(AppLocalization.IsEnglish ? "Body area" : "Testtáj",12,true,color:UI.Muted),1,0);
        Cell(_category,0,1);Cell(_region,1,1);
        Cell(UI.Label("Forrás és állapot",12,true,color:UI.Muted),0,2);
        Cell(_origin,0,3);
        Cell(UI.Label(AppLocalization.IsEnglish ? "Detailed body part" : "Részletes testrész",12,true,color:UI.Muted),1,2);
        Cell(_detailRegion,1,3);
        filterPanel.Children.Add(filterGrid);
        var switches=new HorizontalStackLayout {Spacing=12};
        switches.Children.Add(FilterRow(_availableOnly,"Elérhető eszköz"));
        switches.Children.Add(FilterRow(_favoritesOnly,"Kedvenc"));
        filterPanel.Children.Add(switches);
        // Bound the expanded filters separately: do not put the result list in a
        // ScrollView. Filters must remain reachable even with zero matches.
        var filterScroll = new ScrollView
        {
            Content = filterPanel, IsVisible = false, MaximumHeightRequest = 160,
            VerticalOptions = LayoutOptions.Start
        };
        toggleFilters.Clicked += (_,_) =>
        {
            filterScroll.IsVisible = !filterScroll.IsVisible;
            toggleFilters.Text=filterScroll.IsVisible ? "⚙ Szűrők bezárása" : "⚙ További szűrők";
        };
        header.Children.Add(filterScroll);
        header.Children.Add(_counter);

        var layout = new Grid { RowDefinitions =
        {
            new RowDefinition { Height = GridLength.Auto },
            new RowDefinition { Height = GridLength.Star }
        }};
        layout.Children.Add(header);
        Grid.SetRow(_items, 1);
        layout.Children.Add(_items);
        // Accessible floating shortcut; stays outside the virtualized list.
        var jumpTop = UI.Button("↑ Tetejére", false);
        jumpTop.FontSize = 13;
        jumpTop.HeightRequest = 40;
        jumpTop.Padding = new Thickness(14, 5);
        jumpTop.IsVisible = false;
        jumpTop.HorizontalOptions = LayoutOptions.End;
        jumpTop.VerticalOptions = LayoutOptions.End;
        jumpTop.Margin = new Thickness(0, 0, 18, 22);
        jumpTop.ZIndex = 8;
        jumpTop.Clicked += (_, _) =>
        {
            if (_visible.Count > 0) _items.ScrollTo(0, position:ScrollToPosition.Start, animate:true);
        };
        _items.Scrolled += (_, e) => jumpTop.IsVisible = e.VerticalOffset > 550;
        Grid.SetRow(jumpTop, 1);
        layout.Children.Add(jumpTop);
        Content = layout;
    }

    private static HorizontalStackLayout FilterRow(Switch toggle, string title)
    {
        toggle.Scale=0.75;
        return new() { Spacing=2, Children={toggle,UI.Label(title,12)} };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isAppearing = true;
        // NavigationPage keeps this CatalogPage instance alive. When returning from
        // a detail/3D screen, do not clear the CollectionView or reset its scroll.
        // A revision/favorite change only reconciles the affected visible rows.
        var libraryChanged = EnsureLibrarySnapshot();
        var queryChanged = !string.Equals(_lastAppliedQuery, _search.Text?.Trim() ?? "", StringComparison.Ordinal);
        if (!_presented || libraryChanged || queryChanged || !_lastFavorites.SetEquals(_state.Data.FavoriteIds) ||
            (_availableOnly.IsToggled && !_lastEquipment.SetEquals(_state.Preferences.AvailableEquipment)))
        {
            RefreshMatches(resetScroll: !_presented || queryChanged, fromSearch: queryChanged);
            _presented = true;
        }
    }

    protected override void OnDisappearing()
    {
        _isAppearing = false;
        _searchDelay?.Cancel();
        base.OnDisappearing();
    }

    private bool EnsureLibrarySnapshot()
    {
        // AppState increments LibraryRevision for edits, imports, enable/disable,
        // and archiving. Favorite changes are tracked separately below.
        if (_snapshotReady && _snapshotRevision == _state.Data.LibraryRevision)
            return false;

        _librarySnapshot = _state.LibraryItems
            .Select(x => new LibraryRow(x.Exercise, x.BuiltIn, x.Status)).ToList();
        _snapshotRevision = _state.Data.LibraryRevision;
        _snapshotReady = true;

        // Preserve the selected filters and avoid needlessly resetting pickers
        // every time we return to the screen.
        _refreshing = true;
        try
        {
            UpdatePickerItems(_category,
                ["Összes mozgástípus", .._librarySnapshot.Select(x => x.Exercise.Category)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)]);
            UpdatePickerItems(_region, [AppLocalization.IsEnglish ? "All body areas" : "Összes testtáj",
                ..RegionTaxonomy.Areas.Select(a=>RegionTaxonomy.AreaLabel(a.Id))]);
            UpdatePickerItems(_detailRegion, [AppLocalization.IsEnglish ? "All body parts" : "Összes testrész",
                ..RegionTaxonomy.Details.Select(d=>RegionTaxonomy.DetailLabel(d.Key))]);
        }
        finally { _refreshing = false; }
        return true;
    }

    private static void UpdatePickerItems(Picker picker, IReadOnlyList<string> values)
    {
        if (picker.Items.Count == values.Count &&
            !picker.Items.Where((value, index) => value != values[index]).Any())
            return;
        var previouslySelected = picker.SelectedItem?.ToString();
        picker.Items.Clear();
        foreach (var value in values) picker.Items.Add(value);
        picker.SelectedIndex = Math.Max(0, picker.Items.IndexOf(previouslySelected ?? values[0]));
    }

    private async void DebounceSearch()
    {
        _searchDelay?.Cancel();
        var request = new CancellationTokenSource();
        _searchDelay = request;
        try
        {
            await Task.Delay(180, request.Token);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Check again on the UI thread: a newer keystroke or navigation
                // may have invalidated this request after the delay completed.
                if (_isAppearing && ReferenceEquals(_searchDelay, request) &&
                    !request.IsCancellationRequested &&
                    !string.Equals(_lastAppliedQuery, _search.Text?.Trim() ?? "", StringComparison.Ordinal))
                    RefreshMatches(fromSearch: true);
            });
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_searchDelay, request)) _searchDelay = null;
            request.Dispose();
        }
    }

    private void RefreshMatches(bool resetScroll = true, bool fromSearch = false)
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            // Ignore paging notifications generated by intermediate collection edits.
            _items.RemainingItemsThreshold = -1;
            var query = _search.Text?.Trim() ?? "";
            var category = _category.SelectedItem?.ToString() ?? "Összes mozgástípus";
            var region = _region.SelectedIndex > 0 ? RegionTaxonomy.Areas[_region.SelectedIndex-1].Id : null;
            var detail = _detailRegion.SelectedIndex > 0 ? RegionTaxonomy.Details[_detailRegion.SelectedIndex-1].Key : null;
            var source = _origin.SelectedItem?.ToString() ?? "Minden gyakorlat";
            var favorites = _state.Data.FavoriteIds.ToHashSet(StringComparer.Ordinal);
            var changedFavorites = new HashSet<string>(_lastFavorites, StringComparer.Ordinal);
            changedFavorites.SymmetricExceptWith(favorites);
            var equipment = _state.Preferences.AvailableEquipment.ToHashSet(StringComparer.Ordinal);
            _matches = _librarySnapshot.Where(x =>
                (category == "Összes mozgástípus" || x.Exercise.Category == category) &&
                (region is null || RegionTaxonomy.Matches(x.Exercise,region)) &&
                (detail is null || RegionTaxonomy.Matches(x.Exercise,detail)) &&
                CatalogSearch.MatchesText(x.Exercise, query) &&
                (!_favoritesOnly.IsToggled || favorites.Contains(x.Exercise.Id)) &&
                (!_availableOnly.IsToggled || x.Exercise.Equipment.All(equipment.Contains)) &&
                (source == "Minden gyakorlat" ||
                    (source == "Alapgyakorlatok" && x.BuiltIn) ||
                    (source == "Saját gyakorlatok" && !x.BuiltIn) ||
                    (source == "Piszkozatok" && x.Status == LibraryStatuses.Draft) ||
                    (source == "Kikapcsolt" && x.Status == LibraryStatuses.Disabled)))
                .OrderBy(x => x.Exercise.Category).ThenBy(x => x.Exercise.Name)
                .ToList();

            // A deliberate filter/search starts with one batch; returning from
            // details or changing a card retains the already loaded prefix.
            var count = resetScroll ? BatchSize : Math.Max(BatchSize, _visible.Count);
            CatalogSearch.ReconcilePrefix(_visible, _matches, count,
                row => row.Exercise.Id,
                (current, next) => current.Status != next.Status || current.BuiltIn != next.BuiltIn ||
                    !ReferenceEquals(current.Exercise, next.Exercise) ||
                    changedFavorites.Contains(next.Exercise.Id));

            _lastFavorites = favorites;
            _lastEquipment = equipment;
            _lastAppliedQuery = query;
            _counter.Text = $"{_matches.Count} találat";

            // Only explicit non-search navigation may scroll to the first card.
            // Never move the viewport while the native text editor is active.
            if (resetScroll && !fromSearch && !_search.IsFocused && _visible.Count > 0)
                _items.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
        }
        finally
        {
            _refreshing = false;
            _items.RemainingItemsThreshold = _visible.Count < _matches.Count ? 4 : -1;
        }
    }

    private void LoadNextBatch()
    {
        if (_loading || _refreshing) return;
        _loading=true;
        try
        {
            var start=_visible.Count;
            foreach(var item in _matches.Skip(start).Take(BatchSize)) _visible.Add(item);
            _counter.Text=$"{_matches.Count} találat";
            if(_visible.Count>=_matches.Count) _items.RemainingItemsThreshold=-1;
        }
        finally { _loading=false; }
    }

    private View BuildCard(LibraryRow row)
    {
        var exercise = row.Exercise;
        var layout = new VerticalStackLayout { Spacing=7 };
        var description = new VerticalStackLayout { Spacing=3, Children=
        {
            UI.Label(string.IsNullOrWhiteSpace(exercise.DisplayName)?"Névtelen piszkozat":exercise.DisplayName,15,true),
            UI.Label($"{exercise.Category} · {exercise.RegionAreaLabel}",12,color:UI.Muted),
            UI.Label($"{(row.BuiltIn?"Alap":"Saját")} · {StatusText(row.Status)} · {exercise.TargetLabel}",11,color:UI.Teal)
        }};
        layout.Children.Add(description);
        // Tap anywhere on the card surface to open details. Secondary icon actions
        // are separate native controls and must not navigate to details.
        // Wrapping controls allows narrow screens and large Android text settings
        // without clipping the last buttons or overflowing the card bounds.
        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Center, JustifyContent = FlexJustify.Start
        };
        static Button SmallAction(string glyph,string hint)
        {
            var button = new Button {Text=glyph,FontSize=18,WidthRequest=44,
                MinimumHeightRequest=44, FontAutoScalingEnabled=true,
                Margin=new Thickness(0,0,8,8), Padding=new Thickness(0),
                CornerRadius=12,BackgroundColor=UI.Lavender,
                TextColor=UI.Purple,BorderWidth=0};
            SemanticProperties.SetDescription(button,hint);
            return button;
        }
        // 1. Enable / disable
        if(row.Status is LibraryStatuses.Active or LibraryStatuses.Disabled)
        {
            // Use a labelled toggle with packaged vector artwork, not a Unicode
            // power symbol (some Android system fonts render U+23FB as a blank).
            // The text describes the ACTION; the symbol depicts the CURRENT state.
            var isActive = row.Status == LibraryStatuses.Active;
            var toggle = new Button
            {
                Text = isActive ? "Kikapcsolás" : "Bekapcsolás",
                ImageSource = new FileImageSource
                {
                    File = isActive ? "exercise_toggle_on.png" : "exercise_toggle_off.png"
                },
                ContentLayout = new Button.ButtonContentLayout(
                    Button.ButtonContentLayout.ImagePosition.Left, 4),
                FontSize = 11.5,
                FontAttributes = FontAttributes.Bold,
                MinimumWidthRequest = 115,
                MinimumHeightRequest = 44,
                FontAutoScalingEnabled = true,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8, 6),
                CornerRadius = 12,
                BackgroundColor = isActive ? UI.Lavender : UI.SuccessBackground,
                TextColor = isActive ? UI.Purple : UI.Teal,
                BorderWidth = 0,
                LineBreakMode = LineBreakMode.WordWrap
            };
            SemanticProperties.SetDescription(toggle, isActive
                ? "Aktív gyakorlat. Megnyomva kikapcsolható."
                : "Kikapcsolt gyakorlat. Megnyomva bekapcsolható.");
            toggle.Clicked+=async (_,_)=>
            {
                try
                {
                    if(row.BuiltIn) await _state.SetBuiltInEnabledAsync(exercise.Id,row.Status==LibraryStatuses.Disabled);
                    else await _state.SetCustomEnabledAsync(exercise.Id,row.Status==LibraryStatuses.Disabled);
                    EnsureLibrarySnapshot();
                    RefreshMatches(resetScroll: false);
                }
                catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
            };
            actions.Children.Add(toggle);
        }
        // 2. Favorite (works for both built-in and custom).
        var favorite=SmallAction(_state.IsFavorite(exercise.Id)?"♥":"♡",
            _state.IsFavorite(exercise.Id)?"Kedvenc eltávolítása":"Kedvencekhez adás");
        favorite.Clicked+=async (_,_)=>
        {
            try{await _state.ToggleFavoriteAsync(exercise.Id);RefreshMatches(resetScroll: false);}
            catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
        };
        actions.Children.Add(favorite);
        // 3. Edit, or built-in text/3D illustration correction.
        var edit=SmallAction("✎",row.BuiltIn?"Leírás és ábra javítása":"Gyakorlat szerkesztése");
        edit.Clicked+=async (_,_)=>
        {
            if(row.BuiltIn)
                await Navigation.PushModalAsync(new NavigationPage(new BuiltInContentEditorPage(_state,exercise.Id))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
            else
                await Navigation.PushModalAsync(new NavigationPage(new ExerciseEditorPage(_state,exercise))
                    {BarBackgroundColor=UI.Background,BarTextColor=UI.Ink});
        };
        actions.Children.Add(edit);
        // Only custom items have an extra archive action. Destructive actions
        // remain explicit and never share the main card tap gesture.
        if(!row.BuiltIn)
        {
            var archive=SmallAction("⋯","Saját gyakorlat archiválása");
            archive.Clicked+=async (_,_)=>
            {
                if(!await DisplayAlertAsync("Saját gyakorlat archiválása?", "A korábbi teljesítések megmaradnak.", "Archiválás", "Mégse"))return;
                try {await _state.ArchiveCustomExerciseAsync(exercise.Id);
                    EnsureLibrarySnapshot();RefreshMatches(resetScroll: false);}
                catch(Exception ex){await DisplayAlertAsync("Nem sikerült",ex.Message,"Rendben");}
            };
            actions.Children.Add(archive);
        }
        layout.Children.Add(actions);
        var card=UI.Card(layout);
        var tap=new TapGestureRecognizer();
        tap.Tapped+=async (_,_)=>await Navigation.PushAsync(new ExerciseDetailPage(_state,exercise,fromCatalog:true));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private static string StatusText(string status) => status switch
    {
        LibraryStatuses.Active=>"Aktív", LibraryStatuses.Disabled=>"Kikapcsolt",
        LibraryStatuses.Draft=>"Piszkozat",_=>"Archivált"
    };

    private sealed record LibraryRow(Exercise Exercise,bool BuiltIn,string Status);
}
