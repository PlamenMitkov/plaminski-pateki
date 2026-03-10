import { useEffect, useState } from 'react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import HomePage from './pages/HomePage';
import TrailDetails from './pages/TrailDetails';
import AuthPage from './pages/AuthPage';

const ANIMATION_PREF_STORAGE_KEY = 'ecotrails:animationsEnabled';

function App() {
  const location = useLocation();
  const activeTab = new URLSearchParams(location.search).get('tab');
  const [animationsEnabled, setAnimationsEnabled] = useState(true);

  const isHomeActive = location.pathname === '/' && !activeTab;
  const isTrailsActive =
    location.pathname === '/' && (activeTab === 'list' || activeTab === 'map' || activeTab === 'favorites');
  const isProfileActive = location.pathname === '/auth';
  const isSettingsActive = location.pathname === '/' && activeTab === 'assistant';

  const roundButtonClass = (isActive: boolean) =>
    `round-button${isActive ? ' active-round-button' : ''}`;

  useEffect(() => {
    const storedValue = localStorage.getItem(ANIMATION_PREF_STORAGE_KEY);
    if (storedValue === 'true' || storedValue === 'false') {
      setAnimationsEnabled(storedValue === 'true');
      return;
    }

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    setAnimationsEnabled(!prefersReducedMotion);
  }, []);

  useEffect(() => {
    document.body.classList.toggle('motion-disabled', !animationsEnabled);
    document.body.classList.toggle('motion-enabled', animationsEnabled);
    localStorage.setItem(ANIMATION_PREF_STORAGE_KEY, String(animationsEnabled));

    return () => {
      document.body.classList.remove('motion-disabled');
      document.body.classList.remove('motion-enabled');
    };
  }, [animationsEnabled]);

  return (
    <div className="app-shell">
      <nav className="sidebar-menu" aria-label="Основно меню">
        <Link to="/" className={roundButtonClass(isHomeActive)} title="Начало" aria-label="Начало">
          <i className="fas fa-compass" aria-hidden="true"></i>
        </Link>
        <Link to="/?tab=list" className={roundButtonClass(isTrailsActive)} title="Пътеки" aria-label="Пътеки">
          <i className="fas fa-route" aria-hidden="true"></i>
        </Link>
        <Link to="/auth" className={roundButtonClass(isProfileActive)} title="Профил" aria-label="Профил">
          <i className="fas fa-id-badge" aria-hidden="true"></i>
        </Link>
        <Link
          to="/?tab=assistant"
          className={roundButtonClass(isSettingsActive)}
          title="Настройки"
          aria-label="Настройки"
        >
          <i className="fas fa-sliders" aria-hidden="true"></i>
        </Link>
        <button
          type="button"
          className={`round-button motion-toggle-btn ${animationsEnabled ? 'active-round-button' : ''}`}
          title={animationsEnabled ? 'Анимации: On' : 'Анимации: Off'}
          aria-label={animationsEnabled ? 'Изключи анимациите' : 'Включи анимациите'}
          aria-pressed={animationsEnabled}
          onClick={() => setAnimationsEnabled((current) => !current)}
        >
          <i className={`fas ${animationsEnabled ? 'fa-person-walking' : 'fa-person-running'}`} aria-hidden="true"></i>
        </button>
      </nav>

      <main className="app-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/auth" element={<AuthPage />} />
          <Route path="/trail/:id" element={<TrailDetails />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
