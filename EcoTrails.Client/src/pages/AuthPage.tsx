import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Lock, User } from 'lucide-react';
import { login, register } from '../services/authService';
import '../App.css';

function AuthPage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const submit = async () => {
    try {
      setIsSubmitting(true);
      setError('');

      if (mode === 'login') {
        await login(email, password);
      } else {
        await register(email, password);
      }

      navigate('/');
    } catch (requestError) {
      console.error('Auth error:', requestError);
      setError('Неуспешна автентикация. Провери данните и опитай отново.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="app-container auth-page">
      <div className="auth-card">
        <h1 className="app-title">{mode === 'login' ? 'Вход' : 'Регистрация'}</h1>

        <label className="auth-label">
          <User size={16} />
          Имейл
        </label>
        <input
          className="search-input auth-input"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="you@example.com"
        />

        <label className="auth-label">
          <Lock size={16} />
          Парола
        </label>
        <input
          className="search-input auth-input"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          placeholder="Минимум 6 символа"
        />

        {error && <p className="status-text error">{error}</p>}

        <button type="button" className="primary-btn auth-submit" onClick={submit} disabled={isSubmitting}>
          {isSubmitting ? 'Изпращане...' : mode === 'login' ? 'Вход' : 'Регистрация'}
        </button>

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