import { useEffect, useMemo, useState } from 'react';
import apiClient from '../services/apiClient';
import type { Trail } from '../types/trail';

interface PagedResponse<TItem> {
  items: TItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export type SortBy = 'id' | 'name' | 'difficulty' | 'duration' | 'elevation';
export type SortDirection = 'asc' | 'desc';

interface UseTrailsOptions {
  initialPage?: number;
  initialPageSize?: number;
  initialSearchInput?: string;
  initialSearch?: string;
  initialDifficulty?: number | '';
  initialOnlyWithCoords?: boolean;
  initialMinDurationInput?: string;
  initialMaxDurationInput?: string;
  initialMinElevationInput?: string;
  initialMaxElevationInput?: string;
  initialSortBy?: SortBy;
  initialSortDirection?: SortDirection;
}

function parseOptionalNumber(value: string): number | undefined {
  if (value.trim() === '') {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

export function useTrails(options: UseTrailsOptions = {}) {
  const {
    initialPage = 1,
    initialPageSize = 25,
    initialSearchInput = '',
    initialSearch = '',
    initialDifficulty = '',
    initialOnlyWithCoords = false,
    initialMinDurationInput = '',
    initialMaxDurationInput = '',
    initialMinElevationInput = '',
    initialMaxElevationInput = '',
    initialSortBy = 'id',
    initialSortDirection = 'asc',
  } = options;

  const [trails, setTrails] = useState<Trail[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  const [page, setPage] = useState(initialPage);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [searchInput, setSearchInput] = useState(initialSearchInput);
  const [search, setSearch] = useState(initialSearch);
  const [difficulty, setDifficulty] = useState<number | ''>(initialDifficulty);
  const [onlyWithCoords, setOnlyWithCoords] = useState(initialOnlyWithCoords);
  const [minDurationInput, setMinDurationInput] = useState(initialMinDurationInput);
  const [maxDurationInput, setMaxDurationInput] = useState(initialMaxDurationInput);
  const [minElevationInput, setMinElevationInput] = useState(initialMinElevationInput);
  const [maxElevationInput, setMaxElevationInput] = useState(initialMaxElevationInput);
  const [sortBy, setSortBy] = useState<SortBy>(initialSortBy);
  const [sortDirection, setSortDirection] = useState<SortDirection>(initialSortDirection);
  const [refreshNonce, setRefreshNonce] = useState(0);

  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

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

  const requestParams = useMemo(
    () => ({
      page,
      pageSize,
      ...filterParams,
    }),
    [filterParams, page, pageSize],
  );

  useEffect(() => {
    setIsLoading(true);
    setError('');

    apiClient
      .get<PagedResponse<Trail>>('/trails', {
        params: requestParams,
      })
      .then((response) => {
        setTrails(response.data.items ?? []);
        setTotalCount(response.data.totalCount ?? 0);
        setTotalPages(response.data.totalPages ?? 1);
      })
      .catch((requestError) => {
        console.error('Грешка при зареждане на пътеките:', requestError);
        setTrails([]);
        setTotalCount(0);
        setTotalPages(1);
        setError('Неуспешно зареждане на пътеките.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [requestParams, refreshNonce]);

  const refetch = () => {
    setRefreshNonce((current) => current + 1);
  };

  return {
    trails,
    isLoading,
    error,
    setError,
    totalCount,
    totalPages,
    page,
    setPage,
    pageSize,
    setPageSize,
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
    filterParams,
    refetch,
  };
}
