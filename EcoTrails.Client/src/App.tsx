import { Navigate, Route, Routes } from 'react-router-dom';
import HomePage from './pages/HomePage';
import TrailDetails from './pages/TrailDetails';
import AuthPage from './pages/AuthPage';

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/auth" element={<AuthPage />} />
      <Route path="/trail/:id" element={<TrailDetails />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;
