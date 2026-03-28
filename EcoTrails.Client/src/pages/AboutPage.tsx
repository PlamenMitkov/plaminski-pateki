import '../App.css';

function AboutPage() {
  return (
    <div className="about-page app-container">
      <header className="about-hero">
        <p className="about-kicker">Дипломен проект</p>
        <h1>За нас</h1>
        <p className="about-lead">
          EcoTrails е персонализирана платформа за откриване на екопътеки в България. Проектът съчетава
          картографиране, филтриране, AI асистент и обогатяване на данни, за да подпомогне туристите при планиране
          на преходи.
        </p>
      </header>

      <section className="about-grid" aria-label="Ключова информация">
        <article className="about-card">
          <h2>Мисия</h2>
          <p>
            Да направим избора на подходящ маршрут по-бърз, по-надежден и по-безопасен чрез структурирани данни,
            ясни филтри и лесна визуализация на ключовите характеристики на всяка пътека.
          </p>
        </article>

        <article className="about-card">
          <h2>Какво правим</h2>
          <p>
            Събираме и нормализираме данни за маршрути, автоматизираме обогатяването на описанията и показваме
            резултатите в удобен интерфейс за търсене, карта, любими и детайлен преглед.
          </p>
        </article>

        <article className="about-card">
          <h2>Технологии</h2>
          <p>
            Backend: ASP.NET Core + EF Core + SQL Server. Frontend: React + TypeScript + Vite. Инфраструктура:
            Docker Compose за локално развиване и тестване.
          </p>
        </article>
      </section>

      <section className="about-note" aria-label="Бележка за редакция">
        <h2>Персонализация</h2>
        <p>
          Текстът на тази страница е стартова версия и е подготвен да бъде редактиран според темата и
          представянето на дипломния проект.
        </p>
      </section>
    </div>
  );
}

export default AboutPage;
