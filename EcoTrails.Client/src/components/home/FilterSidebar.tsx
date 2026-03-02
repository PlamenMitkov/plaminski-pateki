import { Download, Heart, MapPin, Search } from 'lucide-react';

type SortBy = 'id' | 'name' | 'difficulty' | 'duration' | 'elevation';
type SortDirection = 'asc' | 'desc';

interface FilterSidebarProps {
  searchInput: string;
  difficulty: number | '';
  sortBy: SortBy;
  sortDirection: SortDirection;
  minDurationInput: string;
  maxDurationInput: string;
  minElevationInput: string;
  maxElevationInput: string;
  onlyWithCoords: boolean;
  shouldShowOnlyFavorites: boolean;
  isExporting: boolean;
  isLoading: boolean;
  onSearchInputChange: (value: string) => void;
  onApplySearch: () => void;
  onDifficultyChange: (value: number | '') => void;
  onSortByChange: (value: SortBy) => void;
  onSortDirectionChange: (value: SortDirection) => void;
  onMinDurationChange: (value: string) => void;
  onMaxDurationChange: (value: string) => void;
  onMinElevationChange: (value: string) => void;
  onMaxElevationChange: (value: string) => void;
  onClearFilters: () => void;
  onToggleOnlyWithCoords: () => void;
  onToggleOnlyFavorites: () => void;
  onExportCsv: () => void;
}

function FilterSidebar({
  searchInput,
  difficulty,
  sortBy,
  sortDirection,
  minDurationInput,
  maxDurationInput,
  minElevationInput,
  maxElevationInput,
  onlyWithCoords,
  shouldShowOnlyFavorites,
  isExporting,
  isLoading,
  onSearchInputChange,
  onApplySearch,
  onDifficultyChange,
  onSortByChange,
  onSortDirectionChange,
  onMinDurationChange,
  onMaxDurationChange,
  onMinElevationChange,
  onMaxElevationChange,
  onClearFilters,
  onToggleOnlyWithCoords,
  onToggleOnlyFavorites,
  onExportCsv,
}: FilterSidebarProps) {
  return (
    <div className="toolbar">
      <div className="search-group">
        <input
          value={searchInput}
          onChange={(event) => onSearchInputChange(event.target.value)}
          placeholder="Търси по име или локация"
          className="search-input"
        />
        <button onClick={onApplySearch} className="primary-btn" type="button">
          <Search size={16} />
          Търси
        </button>
      </div>

      <div className="filter-group">
        <select
          value={difficulty}
          onChange={(event) => {
            const value = event.target.value;
            onDifficultyChange(value === '' ? '' : Number(value));
          }}
          className="select-input"
        >
          <option value="">Всички трудности</option>
          <option value="1">1 - Лесно</option>
          <option value="2">2</option>
          <option value="3">3</option>
          <option value="4">4</option>
          <option value="5">5 - Тежко</option>
        </select>

        <select
          value={sortBy}
          onChange={(event) => onSortByChange(event.target.value as SortBy)}
          className="select-input compact-input"
        >
          <option value="id">Сортиране: ID</option>
          <option value="name">Име</option>
          <option value="difficulty">Трудност</option>
          <option value="duration">Часове</option>
          <option value="elevation">Денивелация</option>
        </select>

        <select
          value={sortDirection}
          onChange={(event) => onSortDirectionChange(event.target.value as SortDirection)}
          className="select-input compact-input"
        >
          <option value="asc">Възходящо</option>
          <option value="desc">Низходящо</option>
        </select>

        <input
          type="number"
          min={0}
          step="0.5"
          value={minDurationInput}
          onChange={(event) => onMinDurationChange(event.target.value)}
          className="select-input compact-input"
          placeholder="Мин. часове"
        />
        <input
          type="number"
          min={0}
          step="0.5"
          value={maxDurationInput}
          onChange={(event) => onMaxDurationChange(event.target.value)}
          className="select-input compact-input"
          placeholder="Макс. часове"
        />

        <input
          type="number"
          min={0}
          step="50"
          value={minElevationInput}
          onChange={(event) => onMinElevationChange(event.target.value)}
          className="select-input compact-input"
          placeholder="Мин. денивелация"
        />
        <input
          type="number"
          min={0}
          step="50"
          value={maxElevationInput}
          onChange={(event) => onMaxElevationChange(event.target.value)}
          className="select-input compact-input"
          placeholder="Макс. денивелация"
        />

        <button onClick={onClearFilters} className="secondary-btn" type="button">
          Изчисти
        </button>
        <button
          onClick={onToggleOnlyWithCoords}
          className={`secondary-btn ${onlyWithCoords ? 'active-btn' : ''}`}
          type="button"
        >
          <MapPin size={16} />
          Само с координати
        </button>
        <button
          onClick={onToggleOnlyFavorites}
          className={`secondary-btn ${shouldShowOnlyFavorites ? 'active-btn' : ''}`}
          type="button"
        >
          <Heart size={16} fill={shouldShowOnlyFavorites ? 'currentColor' : 'none'} />
          Покажи само любими
        </button>
        <button
          onClick={onExportCsv}
          className="secondary-btn export-btn"
          type="button"
          disabled={isExporting || isLoading}
        >
          <Download size={16} />
          {isExporting ? 'Експортиране...' : 'Експорт CSV'}
        </button>
      </div>
    </div>
  );
}

export default FilterSidebar;
