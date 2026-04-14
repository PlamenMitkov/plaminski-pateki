import { useEffect } from 'react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import HomePage from './pages/HomePage';
import TrailDetails from './pages/TrailDetails';
import AuthPage from './pages/AuthPage';
import AboutPage from './pages/AboutPage';
import AdminPanelPage from './pages/AdminPanelPage';

function App() {
  const location = useLocation();
  const activeTab = new URLSearchParams(location.search).get('tab');

  const isHomeActive = location.pathname === '/' && (!activeTab || activeTab === 'home');
  const isTrailsActive =
    location.pathname === '/' && (activeTab === 'list' || activeTab === 'map' || activeTab === 'favorites');
  const isProfileActive = location.pathname === '/auth';
  const isSettingsActive = location.pathname === '/' && activeTab === 'assistant';
  const isAboutActive = location.pathname === '/about';

  const roundButtonClass = (isActive: boolean, extraClass = '') =>
    `round-button${isActive ? ' active-round-button' : ''}${extraClass ? ` ${extraClass}` : ''}`;

  useEffect(() => {
    document.body.classList.remove('motion-disabled');
    document.body.classList.add('motion-enabled');

    return () => {
      document.body.classList.remove('motion-disabled');
      document.body.classList.remove('motion-enabled');
    };
  }, []);

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
        <Link to="/about" className={roundButtonClass(isAboutActive)} title="За нас" aria-label="За нас">
          <i className="fas fa-circle-info" aria-hidden="true"></i>
        </Link>
        <Link
          to="/?tab=assistant"
          className={roundButtonClass(isSettingsActive)}
          title="Настройки"
          aria-label="Настройки"
        >
          <i className="fas fa-sliders" aria-hidden="true"></i>
        </Link>
      </nav>

      <main className="app-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/auth" element={<AuthPage />} />
          <Route path="/about" element={<AboutPage />} />
          <Route path="/trail/:id" element={<TrailDetails />} />
          <Route path="/admin" element={<AdminPanelPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
