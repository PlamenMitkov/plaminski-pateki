export interface DifficultyGaugeProps {
  level: 1 | 2 | 3 | 4 | 5;
}

export function DifficultyGauge({ level }: DifficultyGaugeProps) {
  const getLevelColor = (currentLevel: number): string => {
    if (currentLevel <= 2) return '#4ecca3'; // Eco-Green
    if (currentLevel <= 4) return '#f59e0b'; // Amber-Warning
    return '#ef4444'; // Standard Red
  };

  const activeColor = getLevelColor(level);

  return (
    <div
      className="difficulty-gauge"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '4px',
        marginInline: '8px',
      }}
      title={`Difficulty: ${level}/5`}
    >
      {[1, 2, 3, 4, 5].map((index) => (
        <span
          key={index}
          className={`difficulty-gauge__dot difficulty-gauge__dot--${index <= level ? 'active' : 'inactive'}`}
          style={{
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            backgroundColor: index <= level ? activeColor : '#374151',
            transition: 'background-color 0.2s ease',
          }}
        />
      ))}
      <span
        className="difficulty-gauge__label"
        style={{
          marginLeft: '4px',
          fontSize: '12px',
          fontWeight: 'bold',
          color: activeColor,
        }}
      >
        {level}/5
      </span>
    </div>
  );
}
