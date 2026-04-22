import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from 'react';
import { Mountain, Heart, List, Map as MapIcon, MessageCircle } from 'lucide-react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
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
import FilterSidebar from '../components/home/FilterSidebar';
import { DifficultyGauge } from '../components/common/DifficultyGauge';
import { useAuthCapabilities } from '../hooks/useAuthCapabilities';
import { useAssistant } from '../hooks/useAssistant';
import { useTrails } from '../hooks/useTrails';
import { useFavorites } from '../hooks/useFavorites';
import apiClient from '../services/apiClient';
import {
  createAssistantSession,
  enrichTrailSemanticData,
  getAssistantSessionMessages,
  submitAssistantFeedback,
  type AssistantQuickAction,
} from '../services/assistantService';
import '../App.css';

const LazyAssistantPanel = lazy(() => import('../components/home/AssistantPanel'));
const LazyMapWidget = lazy(() => import('../components/home/MapWidget'));

const ASSISTANT_SESSION_STORAGE_KEY = 'ecotrails:assistantSessionId';
const ASSISTANT_ACTIONS_STORAGE_KEY = 'ecotrails:assistantQuickActionsBySession';
const FORBIDDEN_NOTICE_KEY = 'ecotrails:forbiddenNotice';
const ASSISTANT_FEEDBACK_STORAGE_KEY = 'ecotrails:assistantFeedback';

type HomeTab = 'home' | 'list' | 'map' | 'favorites' | 'assistant';
type SortBy = 'id' | 'name' | 'difficulty' | 'duration' | 'elevation';
type SortDirection = 'asc' | 'desc';

function parseTab(value: string | null): HomeTab {
  if (value === 'home' || value === 'map' || value === 'assistant' || value === 'list') {
    return value;
  }

  if (value === 'favorites') {
    return 'list';
  }

  return 'home';
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

function formatTrailCount(count: number): string {
  return count === 1 ? '1 пътека' : `${count} пътеки`;
}

function formatFilteredTrailCount(count: number): string {
  return count === 1 ? '1 филтрирана пътека' : `${count} филтрирани пътеки`;
}

function formatRouteCount(count: number): string {
  return count === 1 ? '1 маршрут' : `${count} маршрута`;
}

function HomePage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = parseTab(searchParams.get('tab'));

  const {
    trails: fetchedTrails,
    isLoading,
    error,
    page,
    setPage,
    searchInput,
    setSearchInput,
    search,
    setSearch,
    difficulty,
    setDifficulty,
    onlyWithCoords,
    setOnlyWithCoords,
    minDurationInput,
    setMinDurationInput,
    maxDurationInput,
    setMaxDurationInput,
    minElevationInput,
    setMinElevationInput,
    maxElevationInput,
    setMaxElevationInput,
    sortBy,
    setSortBy,
    sortDirection,
    setSortDirection,
    totalPages,
    totalCount,
    filterParams,
  } = useTrails({
    initialPage: Number.isInteger(Number(searchParams.get('page'))) && Number(searchParams.get('page')) > 0
      ? Number(searchParams.get('page'))
      : 1,
    initialPageSize: 25,
    initialSearchInput: searchParams.get('search') ?? '',
    initialSearch: searchParams.get('search') ?? '',
    initialDifficulty: (() => {
      const value = searchParams.get('difficulty');
      if (!value) {
        return '';
      }

      const parsed = Number(value);
      return Number.isInteger(parsed) ? parsed : '';
    })(),
    initialOnlyWithCoords: parseBoolean(searchParams.get('onlyWithCoords')),
    initialMinDurationInput: searchParams.get('minDuration') ?? '',
    initialMaxDurationInput: searchParams.get('maxDuration') ?? '',
    initialMinElevationInput: searchParams.get('minElevation') ?? '',
    initialMaxElevationInput: searchParams.get('maxElevation') ?? '',
    initialSortBy: parseSortBy(searchParams.get('sortBy')),
    initialSortDirection: parseSortDirection(searchParams.get('sortDirection')),
  });

  const [mapFilteredTrails, setMapFilteredTrails] = useState<Trail[]>([]);
  const [showOnlyFavorites, setShowOnlyFavorites] = useState(() => parseBoolean(searchParams.get('onlyFavorites')));
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

  const [isMapLoading, setIsMapLoading] = useState(false);
  const [accessNotice, setAccessNotice] = useState('');
  const { authUser, refreshSession, clearAuth, isAdmin } = useAuthCapabilities();
  const [favoriteTrailsForStats, setFavoriteTrailsForStats] = useState<Trail[]>([]);

  const {
    messages: assistantMessages,
    isTyping,
    sessionId,
    error: chatError,
    sendMessage,
    usedTrails: assistantUsedTrails,
    knowledgeChips: assistantChips,
    quickActions: assistantActions,
    clearChat,
    setSessionId,
    setMessages,
    setQuickActions,
  } = useAssistant();

  const assistantSessionId = sessionId ?? '';
  const [assistantPrompt, setAssistantPrompt] = useState('Препоръчай ми леки маршрути с координати.');
  const [assistantAdminError, setAssistantAdminError] = useState('');
  const [assistantAdminNotice, setAssistantAdminNotice] = useState('');
  const [isAssistantEnriching, setIsAssistantEnriching] = useState(false);
  const [copyStatus, setCopyStatus] = useState('');

  useEffect(() => {
    const storedSessionId = localStorage.getItem(ASSISTANT_SESSION_STORAGE_KEY);
    if (!storedSessionId) {
      return;
    }

    setSessionId(storedSessionId);
  }, [setSessionId]);

  useEffect(() => {
    const notice = sessionStorage.getItem(FORBIDDEN_NOTICE_KEY);
    if (!notice) {
      return;
    }

    setAccessNotice(notice);
    sessionStorage.removeItem(FORBIDDEN_NOTICE_KEY);
  }, []);

  useEffect(() => {
    if (!assistantSessionId) {
      return;
    }

    getAssistantSessionMessages(assistantSessionId, 80)
      .then((messages) => {
        const mapped = messages
          .filter((item) => item.role === 'assistant' || item.role === 'user')
          .map((item) => ({
            id: `history-${item.id}`,
            role: item.role,
            content: item.content,
            timestamp: new Date(item.createdAt),
          }));
        setMessages(mapped);
      })
      .catch((loadError) => {
        console.error('Грешка при зареждане на история на сесията:', loadError);
      });
  }, [assistantSessionId, setMessages]);

  useEffect(() => {
    if (!assistantSessionId) {
      localStorage.removeItem(ASSISTANT_SESSION_STORAGE_KEY);
      return;
    }

    localStorage.setItem(ASSISTANT_SESSION_STORAGE_KEY, assistantSessionId);
  }, [assistantSessionId]);

  useEffect(() => {
    if (!assistantSessionId || assistantActions.length === 0) {
      return;
    }

    try {
      const raw = localStorage.getItem(ASSISTANT_ACTIONS_STORAGE_KEY);
      const parsed = raw ? (JSON.parse(raw) as Record<string, AssistantQuickAction[]>) : {};
      parsed[assistantSessionId] = assistantActions;
      localStorage.setItem(ASSISTANT_ACTIONS_STORAGE_KEY, JSON.stringify(parsed));
    } catch (storageError) {
      console.warn('Неуспешно кеширане на assistant quick actions:', storageError);
    }
  }, [assistantActions, assistantSessionId]);

  useEffect(() => {
    if (!assistantSessionId || assistantActions.length > 0) {
      return;
    }

    try {
      const raw = localStorage.getItem(ASSISTANT_ACTIONS_STORAGE_KEY);
      if (!raw) {
        return;
      }

      const parsed = JSON.parse(raw) as Record<string, AssistantQuickAction[]>;
      const restored = parsed[assistantSessionId];
      if (Array.isArray(restored) && restored.length > 0) {
        setQuickActions(restored);
      }
    } catch (storageError) {
      console.warn('Неуспешно възстановяване на assistant quick actions:', storageError);
    }
  }, [assistantActions.length, assistantSessionId, setQuickActions]);

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

  useEffect(() => {
    setIsMapLoading(true);

    const ids = shouldShowOnlyFavorites ? favoriteIds.join(',') : undefined;

    apiClient
      .get<Trail[]>('/trails/export', {
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

  const trails = shouldShowOnlyFavorites ? mapFilteredTrails : fetchedTrails;

  const selectedMapTrail = selectedMapTrailId
    ? mapFilteredTrails.find((trail) => trail.id === selectedMapTrailId) ?? null
    : null;

  const mapTrailsToShow =
    showOnlySelectedOnMap && selectedMapTrail ? [selectedMapTrail] : mapFilteredTrails;

  const getWeatherLocationCandidate = useCallback((): string => {
    if (selectedMapTrail?.location?.trim()) {
      return selectedMapTrail.location.trim();
    }

    if (search.trim()) {
      return search.trim();
    }

    const firstVisibleTrail = trails[0]?.location?.trim();
    if (firstVisibleTrail) {
      return firstVisibleTrail;
    }

    return 'България';
  }, [search, selectedMapTrail, trails]);

  const pinnedAssistantActions = useMemo<AssistantQuickAction[]>(() => {
    const weatherLocation = getWeatherLocationCandidate();
    return [
      {
        id: 'weather-now',
        label: weatherLocation
          ? `Време сега около ${weatherLocation}`
          : 'Време сега (добави локация)',
        value: weatherLocation,
      },
      {
        id: 'weather-prep',
        label: weatherLocation
          ? `Подготовка за преход около ${weatherLocation}`
          : 'Подготовка според времето',
        value: weatherLocation,
      },
    ];
  }, [getWeatherLocationCandidate]);

  useEffect(() => {
    const nextParams = new URLSearchParams();

    if (activeTab === 'home') {
      nextParams.delete('tab');
    } else {
      nextParams.set('tab', activeTab);
    }
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
    void refreshSession();
  }, [hasToken, refreshSession]);

  useEffect(() => {
    if (favoriteIds.length === 0) {
      setFavoriteTrailsForStats([]);
      return;
    }

    apiClient
      .get<Trail[]>('/trails/export', {
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
    if (tab === 'home') {
      nextParams.delete('tab');
    } else {
      nextParams.set('tab', tab);
    }
    setSearchParams(nextParams, { replace: true });
    setPage(1);
  };

  const buildAssistantFilterSummary = () => {
    return [
      search ? `търсене: ${search}` : null,
      difficulty !== '' ? `трудност: ${difficulty}` : null,
      onlyWithCoords ? 'само с координати' : null,
      minDurationInput.trim() ? `мин. часове: ${minDurationInput.trim()}` : null,
      maxDurationInput.trim() ? `макс. часове: ${maxDurationInput.trim()}` : null,
      minElevationInput.trim() ? `мин. денивелация: ${minElevationInput.trim()}` : null,
      maxElevationInput.trim() ? `макс. денивелация: ${maxElevationInput.trim()}` : null,
      sortBy !== 'id' ? `сортиране: ${sortBy}` : null,
      sortDirection !== 'asc' ? `посока: ${sortDirection}` : null,
    ]
      .filter(Boolean)
      .join(', ');
  };

  const generateAssistantReply = async (promptOverride?: string) => {
    const effectivePrompt = (promptOverride ?? assistantPrompt).trim();
    if (effectivePrompt.length === 0) {
      return;
    }

    if (isLoading) {
      return;
    }

    try {
      await sendMessage(effectivePrompt, {
        filterSummary: buildAssistantFilterSummary(),
        favoriteCount: favoriteIds.length,
        favoriteTrailIds: favoriteIds,
        maxContextTrails: 15,
        onlyWithCoordinates: onlyWithCoords,
      });

      if (!promptOverride) {
        setAssistantPrompt('');
      }
    } catch (requestError) {
      console.error('Грешка при извикване на асистента:', requestError);
    }
  };

  const startNewAssistantSession = async () => {
    try {
      setAssistantAdminError('');
      const session = await createAssistantSession('Нова чат сесия');
      clearChat();
      setSessionId(session.sessionId);
    } catch (requestError) {
      console.error('Грешка при създаване на нова сесия:', requestError);
      setAssistantAdminError('Неуспешно създаване на нова сесия. Опитай отново.');
    }
  };

  const handleAssistantQuickAction = (action: AssistantQuickAction) => {
    if (action.id === 'show-map') {
      const trailId = Number(action.value);
      if (Number.isInteger(trailId) && trailId > 0) {
        setSelectedMapTrailId(trailId);
        setShowOnlySelectedOnMap(true);
      }

      openTab('map');
      return;
    }

    if (action.id === 'weather-now') {
      const targetLocation = action.value?.trim() || getWeatherLocationCandidate();
      const weatherPrompt = `Какво е времето сега около ${targetLocation} и каква е подходяща подготовка за пътеката?`;
      setAssistantPrompt(weatherPrompt);
      void generateAssistantReply(weatherPrompt);
      return;
    }

    if (action.id === 'weather-prep') {
      const targetLocation = action.value?.trim() || getWeatherLocationCandidate();
      const prepPrompt = `Дай конкретна подготовка за преход около ${targetLocation} според времето, терена, водата и нужната екипировка.`;
      setAssistantPrompt(prepPrompt);
      void generateAssistantReply(prepPrompt);
      return;
    }

    if (action.id === 'ask-prompt') {
      const followUpPrompt = action.value?.trim();
      if (!followUpPrompt) {
        return;
      }

      setAssistantPrompt(followUpPrompt);
      void generateAssistantReply(followUpPrompt);
      return;
    }

    if (action.id === 'open-trail-details') {
      const trailId = Number(action.value);
      if (Number.isInteger(trailId) && trailId > 0) {
        navigate(`/trail/${trailId}`);
      }
    }
  };

  const runAdminSemanticEnrichment = async () => {
    try {
      setAssistantAdminError('');
      setAssistantAdminNotice('');
      setIsAssistantEnriching(true);

      const response = await enrichTrailSemanticData({
        limit: 20,
        overwriteExisting: false,
      });

      setAssistantAdminNotice(
        `Обогатяване: обработени ${response.processed}, обновени ${response.updated}, грешки ${response.failed}.`,
      );
    } catch (requestError) {
      console.error('Грешка при admin обогатяване:', requestError);
      setAssistantAdminError('Неуспешно обогатяване на семантични данни. Опитай отново.');
    } finally {
      setIsAssistantEnriching(false);
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

  const mapHandlers = {
    onSelectedTrailChange: (trailId: number | null) => {
      setSelectedMapTrailId(trailId);
      setShowOnlySelectedOnMap(trailId !== null);
    },
    onToggleOnlySelected: () => setShowOnlySelectedOnMap((current) => !current),
    onClearSelected: () => {
      setSelectedMapTrailId(null);
      setShowOnlySelectedOnMap(false);
    },
    onCopyCurrentViewLink: () => {
      void copyCurrentViewLink();
    },
    onSelectTrailFromMap: (trailId: number) => {
      setSelectedMapTrailId(trailId);
      setShowOnlySelectedOnMap(true);
    },
  };

  const assistantHandlers = {
    onPromptChange: (value: string) => setAssistantPrompt(value),
    onGenerateReply: () => {
      void generateAssistantReply();
    },
    onStartNewSession: () => {
      void startNewAssistantSession();
    },
    onOpenMapTab: () => openTab('map'),
    onRunAdminEnrichment: () => {
      void runAdminSemanticEnrichment();
    },
    onQuickAction: (action: AssistantQuickAction) => handleAssistantQuickAction(action),
    onFeedback: (messageId: string, isPositive: boolean) => {
      const persistFeedback = async () => {
        try {
          const raw = localStorage.getItem(ASSISTANT_FEEDBACK_STORAGE_KEY);
          const parsed = raw
            ? (JSON.parse(raw) as Record<string, { value: 'up' | 'down'; at: string; sessionId: string | null }>)
            : {};

          parsed[messageId] = {
            value: isPositive ? 'up' : 'down',
            at: new Date().toISOString(),
            sessionId: assistantSessionId || null,
          };

          localStorage.setItem(ASSISTANT_FEEDBACK_STORAGE_KEY, JSON.stringify(parsed));

          if (assistantSessionId) {
            await submitAssistantFeedback({
              sessionId: assistantSessionId,
              messageId,
              isPositive,
            });
          }

          setAssistantAdminNotice(
            isPositive
              ? 'Благодарим за оценката. Маркирахме отговора като полезен.'
              : 'Получихме обратна връзка. Ще използваме сигнала за подобрение на асистента.',
          );
        } catch (storageError) {
          console.warn('Неуспешно запазване на AI обратна връзка:', storageError);
          setAssistantAdminError('Неуспешно запазване на обратната връзка. Опитай отново.');
        }
      };

      void persistFeedback();
    },
  };

  if (isLoading) {
    return <div>Зареждане на екопътеките...</div>;
  }

  if (error) {
    return <div>Възникна грешка: {error}</div>;
  }

  return (
    <div className="app-container home-page">
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
                clearAuth();
                clearFavorites();
                clearChat();
                localStorage.removeItem(ASSISTANT_SESSION_STORAGE_KEY);
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
          className={`tab-btn ${activeTab === 'home' ? 'active-tab' : ''}`}
          onClick={() => openTab('home')}
        >
          <Mountain size={16} />
          Начало
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'list' ? 'active-tab' : ''}`}
          onClick={() => openTab('list')}
        >
          <List size={16} />
          Пътеки
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'map' ? 'active-tab' : ''}`}
          onClick={() => openTab('map')}
        >
          <MapIcon size={16} />
          Карта
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

      {activeTab === 'home' && (
        <section className="home-welcome-panel">
          <h2>Планирай следващата си разходка по-лесно</h2>
          <p>
            Избери как искаш да започнеш: разгледай пътеки, отвори картата, виж любимите или питай AI
            асистента.
          </p>
          <div className="home-quick-actions">
            <button type="button" className="primary-btn" onClick={() => openTab('list')}>
              Виж всички пътеки
            </button>
            <button type="button" className="secondary-btn" onClick={() => openTab('map')}>
              Отвори картата
            </button>
            <button type="button" className="secondary-btn" onClick={() => openTab('favorites')}>
              Любими
            </button>
            <button type="button" className="secondary-btn" onClick={() => openTab('assistant')}>
              AI асистент
            </button>
          </div>
          <div className="home-stats-grid">
            <article className="home-stat-card">
              <span>Общо пътеки</span>
              <strong>{formatTrailCount(totalCount)}</strong>
            </article>
            <article className="home-stat-card">
              <span>С координати</span>
              <strong>{mapFilteredTrails.filter((trail) => trail.latitude !== null && trail.longitude !== null).length}</strong>
            </article>
            <article className="home-stat-card">
              <span>Любими</span>
              <strong>{formatTrailCount(favoriteIds.length)}</strong>
            </article>
          </div>
        </section>
      )}

      {activeTab !== 'home' && (
        <FilterSidebar
          searchInput={searchInput}
          difficulty={difficulty}
          sortBy={sortBy}
          sortDirection={sortDirection}
          onlyWithCoords={onlyWithCoords}
          shouldShowOnlyFavorites={shouldShowOnlyFavorites}
          onSearchInputChange={(value) => setSearchInput(value)}
          onApplySearch={applySearch}
          onDifficultyChange={(value) => {
            setPage(1);
            setDifficulty(value);
          }}
          onSortByChange={(value) => {
            setPage(1);
            setSortBy(value);
          }}
          onSortDirectionChange={(value) => {
            setPage(1);
            setSortDirection(value);
          }}
          onClearFilters={clearFilters}
          onToggleOnlyWithCoords={() => {
            setPage(1);
            setOnlyWithCoords((current) => !current);
          }}
          onToggleOnlyFavorites={() => {
            setPage(1);
            setShowOnlyFavorites((current) => !current);
          }}
        />
      )}

      {accessNotice && <p className="status-text error">{accessNotice}</p>}
      {error && <p className="status-text error">{error}</p>}
      {isLoading && <p className="status-text">Зареждане...</p>}
      {!error && activeTab !== 'home' && (
        <p className="status-text">
          Пътеки: страница {page} от {totalPages} (Общо {formatTrailCount(totalCount)}) · Карта:{' '}
          {isMapLoading ? 'зареждане...' : formatFilteredTrailCount(mapFilteredTrails.length)}
        </p>
      )}

      {(activeTab === 'map' || activeTab === 'favorites') && (
        <Suspense fallback={<p className="status-text">Зареждане на картата...</p>}>
          <LazyMapWidget
            mapFilteredTrails={mapFilteredTrails}
            mapTrailsToShow={mapTrailsToShow}
            selectedMapTrailId={selectedMapTrailId}
            showOnlySelectedOnMap={showOnlySelectedOnMap}
            copyStatus={copyStatus}
            handlers={mapHandlers}
            mapCenter={mapCenter}
            mapZoom={mapZoom}
            onMapViewChange={(center, zoom) => {
              setMapCenter(center);
              setMapZoom(zoom);
            }}
            formatTrailCount={formatTrailCount}
            formatRouteCount={formatRouteCount}
          />
        </Suspense>
      )}

      {activeTab === 'assistant' && (
        <Suspense fallback={<p className="status-text">Зареждане на асистента...</p>}>
          <LazyAssistantPanel
            assistantPrompt={assistantPrompt}
            isTyping={isTyping}
            isAdmin={isAdmin}
            isAssistantEnriching={isAssistantEnriching}
            assistantSessionId={assistantSessionId}
            assistantChips={assistantChips}
            pinnedAssistantActions={pinnedAssistantActions}
            assistantActions={assistantActions}
            assistantUsedTrails={assistantUsedTrails}
            assistantMessages={assistantMessages}
            assistantAdminNotice={assistantAdminNotice}
            assistantAdminError={assistantAdminError}
            chatError={chatError}
            handlers={assistantHandlers}
            formatTrailCount={formatTrailCount}
          />
        </Suspense>
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
                <DifficultyGauge level={Math.min(Math.max(trail.difficulty, 1), 5) as 1 | 2 | 3 | 4 | 5} />
                <p>
                  <strong>Продължителност:</strong> {trail.durationInHours} ч.
                </p>
                <p>
                  <strong>Денивелация:</strong> {trail.elevationGain} м
                </p>
                <p className="trail-description">{trail.description}</p>
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
