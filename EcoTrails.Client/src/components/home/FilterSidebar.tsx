import { Heart, MapPin, Search } from 'lucide-react';

type SortBy = 'id' | 'name' | 'difficulty' | 'duration' | 'elevation';
type SortDirection = 'asc' | 'desc';

interface FilterSidebarProps {
  searchInput: string;
  difficulty: number | '';
  sortBy: SortBy;
  sortDirection: SortDirection;
  onlyWithCoords: boolean;
  shouldShowOnlyFavorites: boolean;
  onSearchInputChange: (value: string) => void;
  onApplySearch: () => void;
  onDifficultyChange: (value: number | '') => void;
  onSortByChange: (value: SortBy) => void;
  onSortDirectionChange: (value: SortDirection) => void;
  onClearFilters: () => void;
  onToggleOnlyWithCoords: () => void;
  onToggleOnlyFavorites: () => void;
}

function FilterSidebar({
  searchInput,
  difficulty,
  sortBy,
  sortDirection,
  onlyWithCoords,
  shouldShowOnlyFavorites,
  onSearchInputChange,
  onApplySearch,
  onDifficultyChange,
  onSortByChange,
  onSortDirectionChange,
  onClearFilters,
  onToggleOnlyWithCoords,
  onToggleOnlyFavorites,
}: FilterSidebarProps) {
  return (
    <div className="toolbar">
      <div className="search-group">
        <input
          value={searchInput}
          onChange={(event) => onSearchInputChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              onApplySearch();
            }
          }}
          placeholder="Търси по име или локация"
          className="search-input"
        />
        <button onClick={onApplySearch} className="primary-btn search-apply-btn" type="button">
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
      </div>
    </div>
  );
}

export default FilterSidebar;
