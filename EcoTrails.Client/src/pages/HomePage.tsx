import { useEffect, useState } from 'react';
import axios from 'axios';
import { Mountain, Search, Download, Heart, MapPin } from 'lucide-react';
import { Parser } from '@json2csv/plainjs';
import { Link } from 'react-router-dom';
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

function HomePage() {
  const [data, setData] = useState<PagedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [difficulty, setDifficulty] = useState<number | ''>('');
  const [onlyWithCoords, setOnlyWithCoords] = useState(false);
  const [showOnlyFavorites, setShowOnlyFavorites] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [isExporting, setIsExporting] = useState(false);
  const [authUser, setAuthUser] = useState(getAuthUser());
  const [favoriteTrailsForStats, setFavoriteTrailsForStats] = useState<Trail[]>([]);
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

  useEffect(() => {
    setIsLoading(true);
    setError('');

    axios
      .get('http://127.0.0.1:5218/api/trails', {
        params: {
          page,
          pageSize,
          search: search || undefined,
          difficulty: difficulty === '' ? undefined : difficulty,
          onlyWithCoords,
        },
      })
      .then((response) => setData(response.data))
      .catch((requestError) => {
        console.error('Грешка при извличане на данни:', requestError);
        setError('Неуспешно зареждане на пътеките.');
        setData(null);
      })
      .finally(() => setIsLoading(false));
  }, [page, pageSize, search, difficulty, onlyWithCoords]);

  const apiTrails = data?.items ?? [];
  const trails = showOnlyFavorites ? apiTrails.filter((trail) => isFavorite(trail.id)) : apiTrails;
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

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

  const difficultyGroups = [1, 2, 3, 4, 5].map((difficultyValue) => ({
    name: `Трудност ${difficultyValue}`,
    value: favoriteTrailsForStats.filter((trail) => trail.difficulty === difficultyValue).length,
  }));

  const totalElevation = favoriteTrailsForStats.reduce(
    (sum, trail) => sum + trail.elevationGain,
    0,
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
    setPage(1);
  };

  const exportCsv = async () => {
    try {
      setIsExporting(true);

      const ids = showOnlyFavorites ? favoriteIds.join(',') : undefined;

      const response = await axios.get<Trail[]>('http://127.0.0.1:5218/api/trails/export', {
        params: {
          search: search || undefined,
          difficulty: difficulty === '' ? undefined : difficulty,
          onlyWithCoords,
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
            onClick={() => setShowOnlyFavorites((current) => !current)}
            className={`secondary-btn ${showOnlyFavorites ? 'active-btn' : ''}`}
            type="button"
          >
            <Heart size={16} fill={showOnlyFavorites ? 'currentColor' : 'none'} />
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
          Показване на страница {page} от {totalPages} (Общо {totalCount} пътеки)
        </p>
      )}

      <MapComponent trails={trails} />

      {favoriteTrailsForStats.length > 0 && (
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
    </div>
  );
}

export default HomePage;