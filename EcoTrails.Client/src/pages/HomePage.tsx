import { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { Mountain, Search, Download, Heart, MapPin, List, Map, MessageCircle } from 'lucide-react';
import { Parser } from '@json2csv/plainjs';
import { Link, useSearchParams } from 'react-router-dom';
import {
  Bar,
  BarChart,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { Trail } from '../types/trail';
import MapComponent from '../components/MapComponent';
import { useFavorites } from '../hooks/useFavorites';
import { getAuthUser, logout } from '../services/authService';
import '../App.css';

interface PagedResponse {
  items: Trail[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

type HomeTab = 'list' | 'map' | 'favorites' | 'assistant';
type SortBy = 'id' | 'name' | 'difficulty' | 'duration' | 'elevation';
type SortDirection = 'asc' | 'desc';

function parseTab(value: string | null): HomeTab {
  if (value === 'map' || value === 'favorites' || value === 'assistant' || value === 'list') {
    return value;
  }

  return 'list';
}

function parseOptionalNumber(value: string): number | undefined {
  if (value.trim() === '') {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function parseBoolean(value: string | null, fallback = false): boolean {
  if (value === null) {
    return fallback;
  }

  return value === 'true';
}

function parseSortBy(value: string | null): SortBy {
  if (value === 'name' || value === 'difficulty' || value === 'duration' || value === 'elevation') {
    return value;
  }

  return 'id';
}

function parseSortDirection(value: string | null): SortDirection {
  return value === 'desc' ? 'desc' : 'asc';
}

function parseMapCoordinate(value: string | null, fallback: number): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function parseMapZoom(value: string | null, fallback = 7): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }

  return Math.min(Math.max(Math.round(parsed), 3), 17);
}

function HomePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = parseTab(searchParams.get('tab'));

  const [data, setData] = useState<PagedResponse | null>(null);
  const [mapFilteredTrails, setMapFilteredTrails] = useState<Trail[]>([]);
  const [page, setPage] = useState(() => {
    const value = Number(searchParams.get('page'));
    return Number.isInteger(value) && value > 0 ? value : 1;
  });
  const [pageSize] = useState(25);
  const [searchInput, setSearchInput] = useState(() => searchParams.get('search') ?? '');
  const [search, setSearch] = useState(() => searchParams.get('search') ?? '');
  const [difficulty, setDifficulty] = useState<number | ''>(() => {
    const value = searchParams.get('difficulty');
    if (!value) {
      return '';
    }

    const parsed = Number(value);
    return Number.isInteger(parsed) ? parsed : '';
  });
  const [onlyWithCoords, setOnlyWithCoords] = useState(() => parseBoolean(searchParams.get('onlyWithCoords')));
  const [showOnlyFavorites, setShowOnlyFavorites] = useState(() => parseBoolean(searchParams.get('onlyFavorites')));
  const [minDurationInput, setMinDurationInput] = useState(() => searchParams.get('minDuration') ?? '');
  const [maxDurationInput, setMaxDurationInput] = useState(() => searchParams.get('maxDuration') ?? '');
  const [minElevationInput, setMinElevationInput] = useState(() => searchParams.get('minElevation') ?? '');
  const [maxElevationInput, setMaxElevationInput] = useState(() => searchParams.get('maxElevation') ?? '');
  const [sortBy, setSortBy] = useState<SortBy>(() => parseSortBy(searchParams.get('sortBy')));
  const [sortDirection, setSortDirection] = useState<SortDirection>(() =>
    parseSortDirection(searchParams.get('sortDirection')),
  );
  const [selectedMapTrailId, setSelectedMapTrailId] = useState<number | null>(() => {
    const value = Number(searchParams.get('selectedTrailId'));
    return Number.isInteger(value) && value > 0 ? value : null;
  });
  const [showOnlySelectedOnMap, setShowOnlySelectedOnMap] = useState(() =>
    parseBoolean(searchParams.get('onlySelectedOnMap')),
  );
  const [mapCenter, setMapCenter] = useState<[number, number]>(() => [
    parseMapCoordinate(searchParams.get('mapLat'), 42.7),
    parseMapCoordinate(searchParams.get('mapLng'), 25.3),
  ]);
  const [mapZoom, setMapZoom] = useState(() => parseMapZoom(searchParams.get('mapZoom')));

  const [isLoading, setIsLoading] = useState(false);
  const [isMapLoading, setIsMapLoading] = useState(false);
  const [error, setError] = useState('');
  const [isExporting, setIsExporting] = useState(false);
  const [authUser, setAuthUser] = useState(getAuthUser());
  const [favoriteTrailsForStats, setFavoriteTrailsForStats] = useState<Trail[]>([]);
  const [assistantPrompt, setAssistantPrompt] = useState('Препоръчай ми леки маршрути с координати.');
  const [assistantReply, setAssistantReply] = useState('');
  const [copyStatus, setCopyStatus] = useState('');

  const {
    favoriteIds,
    isFavorite,
    toggleFavorite,
    hasToken,
    hasPendingCloudSync,
    syncFavoritesToCloud,
    isSyncing,
    lastSyncError,
    clearFavorites,
  } = useFavorites();

  const shouldShowOnlyFavorites = showOnlyFavorites || activeTab === 'favorites';

  const filterParams = useMemo(
    () => ({
      search: search || undefined,
      difficulty: difficulty === '' ? undefined : difficulty,
      onlyWithCoords,
      minDuration: parseOptionalNumber(minDurationInput),
      maxDuration: parseOptionalNumber(maxDurationInput),
      minElevation: parseOptionalNumber(minElevationInput),
      maxElevation: parseOptionalNumber(maxElevationInput),
      sortBy: sortBy === 'id' ? undefined : sortBy,
      sortDirection,
    }),
    [
      search,
      difficulty,
      onlyWithCoords,
      minDurationInput,
      maxDurationInput,
      minElevationInput,
      maxElevationInput,
      sortBy,
      sortDirection,
    ],
  );

  useEffect(() => {
    setIsLoading(true);
    setError('');

    axios
      .get('http://127.0.0.1:5218/api/trails', {
        params: {
          page,
          pageSize,
          ...filterParams,
        },
      })
      .then((response) => setData(response.data))
      .catch((requestError) => {
        console.error('Грешка при извличане на данни:', requestError);
        setError('Неуспешно зареждане на пътеките.');
        setData(null);
      })
      .finally(() => setIsLoading(false));
  }, [page, pageSize, filterParams]);

  useEffect(() => {
    setIsMapLoading(true);

    const ids = shouldShowOnlyFavorites ? favoriteIds.join(',') : undefined;

    axios
      .get<Trail[]>('http://127.0.0.1:5218/api/trails/export', {
        params: {
          ...filterParams,
          ids,
        },
      })
      .then((response) => setMapFilteredTrails(response.data))
      .catch((requestError) => {
        console.error('Грешка при извличане на картирани данни:', requestError);
        setMapFilteredTrails([]);
      })
      .finally(() => setIsMapLoading(false));
  }, [filterParams, shouldShowOnlyFavorites, favoriteIds]);

  useEffect(() => {
    if (!selectedMapTrailId) {
      return;
    }

    const exists = mapFilteredTrails.some((trail) => trail.id === selectedMapTrailId);
    if (!exists) {
      setSelectedMapTrailId(null);
      setShowOnlySelectedOnMap(false);
    }
  }, [mapFilteredTrails, selectedMapTrailId]);

  const apiTrails = data?.items ?? [];
  const trails = shouldShowOnlyFavorites ? apiTrails.filter((trail) => isFavorite(trail.id)) : apiTrails;
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  const selectedMapTrail = selectedMapTrailId
    ? mapFilteredTrails.find((trail) => trail.id === selectedMapTrailId) ?? null
    : null;

  const mapTrailsToShow =
    showOnlySelectedOnMap && selectedMapTrail ? [selectedMapTrail] : mapFilteredTrails;

  useEffect(() => {
    const nextParams = new URLSearchParams();

    nextParams.set('tab', activeTab);
    nextParams.set('page', String(page));

    if (search.trim() !== '') nextParams.set('search', search.trim());
    if (difficulty !== '') nextParams.set('difficulty', String(difficulty));
    if (onlyWithCoords) nextParams.set('onlyWithCoords', 'true');
    if (showOnlyFavorites) nextParams.set('onlyFavorites', 'true');
    if (minDurationInput.trim() !== '') nextParams.set('minDuration', minDurationInput.trim());
    if (maxDurationInput.trim() !== '') nextParams.set('maxDuration', maxDurationInput.trim());
    if (minElevationInput.trim() !== '') nextParams.set('minElevation', minElevationInput.trim());
    if (maxElevationInput.trim() !== '') nextParams.set('maxElevation', maxElevationInput.trim());
    if (sortBy !== 'id') nextParams.set('sortBy', sortBy);
    if (sortDirection !== 'asc') nextParams.set('sortDirection', sortDirection);
    if (selectedMapTrailId) nextParams.set('selectedTrailId', String(selectedMapTrailId));
    if (showOnlySelectedOnMap) nextParams.set('onlySelectedOnMap', 'true');
    if (mapCenter[0] !== 42.7) nextParams.set('mapLat', mapCenter[0].toFixed(6));
    if (mapCenter[1] !== 25.3) nextParams.set('mapLng', mapCenter[1].toFixed(6));
    if (mapZoom !== 7) nextParams.set('mapZoom', String(mapZoom));

    const current = searchParams.toString();
    const next = nextParams.toString();
    if (current !== next) {
      setSearchParams(nextParams, { replace: true });
    }
  }, [
    activeTab,
    page,
    search,
    difficulty,
    onlyWithCoords,
    showOnlyFavorites,
    minDurationInput,
    maxDurationInput,
    minElevationInput,
    maxElevationInput,
    sortBy,
    sortDirection,
    selectedMapTrailId,
    showOnlySelectedOnMap,
    mapCenter,
    mapZoom,
    searchParams,
    setSearchParams,
  ]);

  useEffect(() => {
    const currentUser = getAuthUser();
    setAuthUser(currentUser);
  }, [hasToken]);

  useEffect(() => {
    if (favoriteIds.length === 0) {
      setFavoriteTrailsForStats([]);
      return;
    }

    axios
      .get<Trail[]>('http://127.0.0.1:5218/api/trails/export', {
        params: { ids: favoriteIds.join(',') },
      })
      .then((response) => setFavoriteTrailsForStats(response.data))
      .catch((requestError) => {
        console.error('Грешка при зареждане на статистика:', requestError);
        setFavoriteTrailsForStats([]);
      });
  }, [favoriteIds]);

  const difficultyGroups = useMemo(
    () =>
      [1, 2, 3, 4, 5].map((difficultyValue) => ({
        name: `Трудност ${difficultyValue}`,
        value: favoriteTrailsForStats.filter((trail) => trail.difficulty === difficultyValue).length,
      })),
    [favoriteTrailsForStats],
  );

  const totalElevation = useMemo(
    () => favoriteTrailsForStats.reduce((sum, trail) => sum + trail.elevationGain, 0),
    [favoriteTrailsForStats],
  );

  const elevationData = [{ name: 'Общо', elevation: totalElevation }];

  const applySearch = () => {
    setPage(1);
    setSearch(searchInput.trim());
  };

  const clearFilters = () => {
    setSearchInput('');
    setSearch('');
    setDifficulty('');
    setOnlyWithCoords(false);
    setShowOnlyFavorites(false);
    setMinDurationInput('');
    setMaxDurationInput('');
    setMinElevationInput('');
    setMaxElevationInput('');
    setSortBy('id');
    setSortDirection('asc');
    setSelectedMapTrailId(null);
    setShowOnlySelectedOnMap(false);
    setPage(1);
  };

  const openTab = (tab: HomeTab) => {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set('tab', tab);
    setSearchParams(nextParams, { replace: true });
    setPage(1);
  };

  const generateAssistantReply = () => {
    if (isLoading) {
      setAssistantReply('Все още зареждам пътеките. Изчакай няколко секунди и опитай отново.');
      return;
    }

    if (error) {
      setAssistantReply('Има проблем със зареждането на данните. Натисни Изчисти след презареждане.');
      return;
    }

    const preview = trails.slice(0, 3).map((trail) => `${trail.name} (${trail.location})`);
    const withCoordsCount = trails.filter(
      (trail) => trail.latitude !== null && trail.longitude !== null,
    ).length;

    if (trails.length === 0) {
      setAssistantReply(
        `Няма резултати за текущите филтри. Опитай по-широко търсене или друга трудност. Въпрос: ${assistantPrompt}`,
      );
      return;
    }

    setAssistantReply(
      `Имаш ${trails.length} резултата на текущата страница (${withCoordsCount} с координати). ` +
        `Подходящи за старт: ${preview.join(', ')}. ` +
        `Въпрос: ${assistantPrompt}`,
    );
  };

  const exportCsv = async () => {
    try {
      setIsExporting(true);

      const ids = shouldShowOnlyFavorites ? favoriteIds.join(',') : undefined;

      const response = await axios.get<Trail[]>('http://127.0.0.1:5218/api/trails/export', {
        params: {
          ...filterParams,
          ids,
        },
      });

      const parser = new Parser({
        fields: [
          'id',
          'name',
          'location',
          'difficulty',
          'durationInHours',
          'elevationGain',
          'latitude',
          'longitude',
          'description',
        ],
      });

      const csv = parser.parse(response.data);
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'ecotrails-export.csv';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch (exportError) {
      console.error('Грешка при експорт на CSV:', exportError);
      setError('Неуспешен експорт на CSV.');
    } finally {
      setIsExporting(false);
    }
  };

  const copyCurrentViewLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopyStatus('Линкът е копиран.');
    } catch {
      setCopyStatus('Неуспешно копиране на линка.');
    }

    window.setTimeout(() => {
      setCopyStatus('');
    }, 2200);
  };

  return (
    <div className="app-container">
      <h1 className="app-title">
        <Mountain size={32} />
        Екопътеки България
      </h1>

      <div className="auth-toolbar">
        {authUser ? (
          <>
            <span className="auth-user">Влязъл: {authUser.email}</span>
            <button
              type="button"
              className="secondary-btn"
              onClick={() => {
                logout();
                clearFavorites();
                setAuthUser(null);
              }}
            >
              Изход
            </button>
          </>
        ) : (
          <Link className="secondary-btn auth-link-btn" to="/auth">
            Вход / Регистрация
          </Link>
        )}
      </div>

      {hasPendingCloudSync && (
        <div className="sync-banner">
          <p>Намерихме любими пътеки в браузъра ви. Искате ли да ги запазите в профила си?</p>
          <button
            type="button"
            className="primary-btn"
            onClick={() => void syncFavoritesToCloud()}
            disabled={isSyncing}
          >
            {isSyncing ? 'Синхронизиране...' : 'Синхронизирай любими'}
          </button>
        </div>
      )}

      {lastSyncError && <p className="status-text error">{lastSyncError}</p>}

      <div className="tabs-nav">
        <button
          type="button"
          className={`tab-btn ${activeTab === 'list' ? 'active-tab' : ''}`}
          onClick={() => openTab('list')}
        >
          <List size={16} />
          Списък
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'map' ? 'active-tab' : ''}`}
          onClick={() => openTab('map')}
        >
          <Map size={16} />
          Карта
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'favorites' ? 'active-tab' : ''}`}
          onClick={() => openTab('favorites')}
        >
          <Heart size={16} fill={activeTab === 'favorites' ? 'currentColor' : 'none'} />
          Любими
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'assistant' ? 'active-tab' : ''}`}
          onClick={() => openTab('assistant')}
        >
          <MessageCircle size={16} />
          Асистент
        </button>
      </div>

      <div className="toolbar">
        <div className="search-group">
          <input
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Търси по име или локация"
            className="search-input"
          />
          <button onClick={applySearch} className="primary-btn" type="button">
            <Search size={16} />
            Търси
          </button>
        </div>

        <div className="filter-group">
          <select
            value={difficulty}
            onChange={(event) => {
              const value = event.target.value;
              setPage(1);
              setDifficulty(value === '' ? '' : Number(value));
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
            onChange={(event) => {
              setPage(1);
              setSortBy(parseSortBy(event.target.value));
            }}
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
            onChange={(event) => {
              setPage(1);
              setSortDirection(parseSortDirection(event.target.value));
            }}
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
            onChange={(event) => {
              setPage(1);
              setMinDurationInput(event.target.value);
            }}
            className="select-input compact-input"
            placeholder="Мин. часове"
          />
          <input
            type="number"
            min={0}
            step="0.5"
            value={maxDurationInput}
            onChange={(event) => {
              setPage(1);
              setMaxDurationInput(event.target.value);
            }}
            className="select-input compact-input"
            placeholder="Макс. часове"
          />

          <input
            type="number"
            min={0}
            step="50"
            value={minElevationInput}
            onChange={(event) => {
              setPage(1);
              setMinElevationInput(event.target.value);
            }}
            className="select-input compact-input"
            placeholder="Мин. денивелация"
          />
          <input
            type="number"
            min={0}
            step="50"
            value={maxElevationInput}
            onChange={(event) => {
              setPage(1);
              setMaxElevationInput(event.target.value);
            }}
            className="select-input compact-input"
            placeholder="Макс. денивелация"
          />

          <button onClick={clearFilters} className="secondary-btn" type="button">
            Изчисти
          </button>
          <button
            onClick={() => {
              setPage(1);
              setOnlyWithCoords((current) => !current);
            }}
            className={`secondary-btn ${onlyWithCoords ? 'active-btn' : ''}`}
            type="button"
          >
            <MapPin size={16} />
            Само с координати
          </button>
          <button
            onClick={() => {
              setPage(1);
              setShowOnlyFavorites((current) => !current);
            }}
            className={`secondary-btn ${shouldShowOnlyFavorites ? 'active-btn' : ''}`}
            type="button"
          >
            <Heart size={16} fill={shouldShowOnlyFavorites ? 'currentColor' : 'none'} />
            Покажи само любими
          </button>
          <button
            onClick={exportCsv}
            className="secondary-btn export-btn"
            type="button"
            disabled={isExporting || isLoading}
          >
            <Download size={16} />
            {isExporting ? 'Експортиране...' : 'Експорт CSV'}
          </button>
        </div>
      </div>

      {error && <p className="status-text error">{error}</p>}
      {isLoading && <p className="status-text">Зареждане...</p>}
      {!error && (
        <p className="status-text">
          Списък: страница {page} от {totalPages} (Общо {totalCount} пътеки) · Карта:{' '}
          {isMapLoading ? 'зареждане...' : `${mapFilteredTrails.length} филтрирани пътеки`}
        </p>
      )}

      {(activeTab === 'map' || activeTab === 'favorites') && (
        <>
          <div className="map-tools">
            <select
              className="select-input map-select"
              value={selectedMapTrailId ?? ''}
              onChange={(event) => {
                const value = event.target.value;
                if (value === '') {
                  setSelectedMapTrailId(null);
                  setShowOnlySelectedOnMap(false);
                  return;
                }

                setSelectedMapTrailId(Number(value));
                setShowOnlySelectedOnMap(true);
              }}
            >
              <option value="">Избери пътека на картата...</option>
              {mapFilteredTrails.map((trail) => (
                <option key={trail.id} value={trail.id}>
                  {trail.name}
                </option>
              ))}
            </select>
            <button
              type="button"
              className={`secondary-btn ${showOnlySelectedOnMap ? 'active-btn' : ''}`}
              disabled={!selectedMapTrailId}
              onClick={() => setShowOnlySelectedOnMap((current) => !current)}
            >
              Покажи само избраната
            </button>
            <button
              type="button"
              className="secondary-btn"
              disabled={!selectedMapTrailId}
              onClick={() => {
                setSelectedMapTrailId(null);
                setShowOnlySelectedOnMap(false);
              }}
            >
              Изчисти избора
            </button>
            <button type="button" className="secondary-btn" onClick={() => void copyCurrentViewLink()}>
              Копирай линк към текущия изглед
            </button>
            <span className="map-counter">
              На картата: {mapTrailsToShow.length}/{mapFilteredTrails.length}
            </span>
            {copyStatus && <span className="map-copy-status">{copyStatus}</span>}
          </div>

          <MapComponent
            trails={mapTrailsToShow}
            selectedTrailId={selectedMapTrailId}
            onSelectTrail={(trailId) => {
              setSelectedMapTrailId(trailId);
              setShowOnlySelectedOnMap(true);
            }}
            initialCenter={mapCenter}
            initialZoom={mapZoom}
            onMapViewChange={(center, zoom) => {
              setMapCenter(center);
              setMapZoom(zoom);
            }}
          />
        </>
      )}

      {activeTab === 'assistant' && (
        <div className="assistant-card">
          <h3>Планински асистент</h3>
          <p>
            Използва текущите филтри, резултатите от страницата и любимите ти пътеки, за да ти даде
            бърза препоръка.
          </p>
          <input
            value={assistantPrompt}
            onChange={(event) => setAssistantPrompt(event.target.value)}
            className="search-input assistant-input"
            placeholder="Напр. препоръчай ми кратка пътека около София"
          />
          <div className="assistant-actions">
            <button type="button" className="primary-btn" onClick={generateAssistantReply}>
              Генерирай препоръка
            </button>
            <button type="button" className="secondary-btn" onClick={() => openTab('map')}>
              Към карта
            </button>
            <button type="button" className="secondary-btn" onClick={() => openTab('favorites')}>
              Към любими
            </button>
          </div>
          {assistantReply && <p className="assistant-reply">{assistantReply}</p>}
        </div>
      )}

      {activeTab === 'favorites' && favoriteTrailsForStats.length > 0 && (
        <div className="dashboard-grid">
          <div className="chart-card">
            <h3>Любими по трудност</h3>
            <ResponsiveContainer width="100%" height={260}>
              <PieChart>
                <Pie data={difficultyGroups} dataKey="value" nameKey="name" outerRadius={90} label>
                  {difficultyGroups.map((group, index) => (
                    <Cell
                      key={group.name}
                      fill={['#22c55e', '#84cc16', '#f59e0b', '#f97316', '#ef4444'][index]}
                    />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          </div>

          <div className="chart-card">
            <h3>Обща денивелация на любими</h3>
            <ResponsiveContainer width="100%" height={260}>
              <BarChart data={elevationData}>
                <XAxis dataKey="name" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="elevation" fill="#3b82f6" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {(activeTab === 'list' || activeTab === 'favorites') && (
        <>
          <div className="trails-grid">
            {trails.map((trail) => (
              <div key={trail.id} className="trail-card">
                <h2>{trail.name}</h2>
                <p>
                  <strong>Локация:</strong> {trail.location}
                </p>
                <p>
                  <strong>Трудност:</strong> {trail.difficulty}/5
                </p>
                <p>
                  <strong>Продължителност:</strong> {trail.durationInHours} ч.
                </p>
                <p>
                  <strong>Денивелация:</strong> {trail.elevationGain} м
                </p>
                <p>{trail.description}</p>
                <button
                  type="button"
                  className={`secondary-btn favorite-btn ${isFavorite(trail.id) ? 'active-btn' : ''}`}
                  onClick={() => toggleFavorite(trail.id)}
                >
                  <Heart size={16} fill={isFavorite(trail.id) ? 'currentColor' : 'none'} />
                  {isFavorite(trail.id) ? 'Премахни от любими' : 'Добави в любими'}
                </button>
                <Link className="trail-link" to={`/trail/${trail.id}`}>
                  Виж детайли
                </Link>
              </div>
            ))}

            {!isLoading && trails.length === 0 && !error && (
              <p className="status-text">Няма резултати за избраните филтри.</p>
            )}
          </div>

          {activeTab === 'list' && (
            <div className="pagination">
              <button
                type="button"
                className="secondary-btn"
                onClick={() => setPage((currentPage) => Math.max(currentPage - 1, 1))}
                disabled={page === 1 || isLoading}
              >
                Previous
              </button>
              <span>Страница {page}</span>
              <button
                type="button"
                className="secondary-btn"
                onClick={() => setPage((currentPage) => currentPage + 1)}
                disabled={isLoading || page >= totalPages}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default HomePage;
