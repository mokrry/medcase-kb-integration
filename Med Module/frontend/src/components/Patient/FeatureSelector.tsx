interface FeatureSelectorProps {
  includeComplaintsFeatures: boolean;
  includeAnamnesisFeatures: boolean;
  onIncludeComplaintsChange: (value: boolean) => void;
  onIncludeAnamnesisChange: (value: boolean) => void;
}

export function FeatureSelector(props: FeatureSelectorProps) {
  const {
    includeComplaintsFeatures,
    includeAnamnesisFeatures,
    onIncludeComplaintsChange,
    onIncludeAnamnesisChange
  } = props;

  return (
    <section className="panel">
      <h3>Архивный сценарий словарей</h3>
      <p className="muted">
        Этот блок сохранён для обратной совместимости. Основной способ задания сущностей перенесён на страницу
        «Извлечение» и теперь работает через текстовый ввод.
      </p>

      <div className="checkbox-group">
        <label className="checkbox">
          <input
            type="checkbox"
            checked={includeComplaintsFeatures}
            onChange={(event) => onIncludeComplaintsChange(event.target.checked)}
          />
          <span>Искать признаки из словаря жалоб</span>
        </label>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={includeAnamnesisFeatures}
            onChange={(event) => onIncludeAnamnesisChange(event.target.checked)}
          />
          <span>Искать признаки из словаря анамнеза</span>
        </label>
      </div>
    </section>
  );
}
