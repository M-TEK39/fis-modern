export function VehicleEditTariffFields() {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-edit-tariff-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Optional calculation</p>
          <h2 id="vehicle-edit-tariff-title">Tariff components</h2>
        </div>
      </div>
      <label className="vehicle-checkbox-label" htmlFor="recalculateTariff">
        <input id="recalculateTariff" name="recalculateTariff" type="checkbox" value="true" />
        Recalculate vehicle tariff components on submit
      </label>
    </section>
  );
}
