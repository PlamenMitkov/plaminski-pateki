import { useState } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { Lock, User } from 'lucide-react';
import axios from 'axios';
import { login, register } from '../services/authService';
import { useAuthCapabilities } from '../hooks/useAuthCapabilities';
import '../App.css';

type AuthMode = 'login' | 'register';

function AuthPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const sessionExpired = searchParams.get('sessionExpired') === 'true';
  const { authUser, refreshSession } = useAuthCapabilities();

  const [mode, setMode] = useState<AuthMode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const submit = async () => {
    try {
      setIsSubmitting(true);
      setError('');

      if (!email.trim()) {
        setError('Въведи валиден имейл.');
        return;
      }

      if (!password.trim()) {
        setError('Въведи парола.');
        return;
      }

      if (mode === 'register') {
        if (password.length < 6) {
          setError('Паролата трябва да е поне 6 символа.');
          return;
        }

        if (!/\d/.test(password)) {
          setError('Паролата трябва да съдържа поне една цифра.');
          return;
        }
      }

      if (mode === 'login') {
        await login(email, password);
      } else {
        await register(email, password);
      }

      await refreshSession();
      navigate('/', { replace: true });
    } catch (requestError) {
      console.error('Auth error:', requestError);

      if (axios.isAxiosError(requestError)) {
        const status = requestError.response?.status;
        const responseData = requestError.response?.data;

        if (Array.isArray(responseData) && responseData.length > 0) {
          setError(responseData.join(' '));
        } else if (typeof responseData === 'string' && responseData.trim().length > 0) {
          setError(responseData);
        } else if (status === 409) {
          setError('Потребител с този имейл вече съществува.');
        } else if (status === 429) {
          setError('Прекалено много опити. Изчакай малко и опитай отново.');
        } else {
          setError('Неуспешна автентикация. Провери данните и опитай отново.');
        }
      } else {
        setError('Неуспешна автентикация. Провери данните и опитай отново.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (authUser) {
    return <Navigate to="/user" replace />;
  }

  return (
    <div className="app-container auth-page">
      <div className="auth-card">
        <h1 className="app-title">{mode === 'login' ? 'Вход' : 'Регистрация'}</h1>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          {sessionExpired && <p className="status-text error">Сесията е изтекла. Влез отново.</p>}

          <label className="auth-label" htmlFor="auth-email">
            <User size={16} />
            Имейл
          </label>
          <input
            id="auth-email"
            className="search-input auth-input"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
          />

          <label className="auth-label" htmlFor="auth-password">
            <Lock size={16} />
            Парола
          </label>
          <input
            id="auth-password"
            className="search-input auth-input"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="Минимум 6 символа"
          />

          {error && <p className="status-text error">{error}</p>}

          <button type="submit" className="primary-btn auth-submit" disabled={isSubmitting}>
            {isSubmitting ? 'Изпращане...' : mode === 'login' ? 'Вход' : 'Регистрация'}
          </button>
        </form>

        <button
          type="button"
          className="secondary-btn"
          onClick={() => setMode((current) => (current === 'login' ? 'register' : 'login'))}
        >
          {mode === 'login' ? 'Нямаш акаунт? Регистрация' : 'Имаш акаунт? Вход'}
        </button>

        <Link className="trail-link" to="/">
          Назад към началото
        </Link>
      </div>
    </div>
  );
}

export default AuthPage;
