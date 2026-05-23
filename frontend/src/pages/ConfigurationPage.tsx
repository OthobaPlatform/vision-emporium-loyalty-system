import { useState } from 'react';
import { CycleConfigSection } from './configuration/CycleConfigSection';
import { ThresholdsConfigSection } from './configuration/ThresholdsConfigSection';
import { GeneralConfigSection } from './configuration/GeneralConfigSection';

type ConfigTab = 'cycle' | 'thresholds' | 'general';

export function ConfigurationPage() {
  const [activeTab, setActiveTab] = useState<ConfigTab>('cycle');

  const tabs: { id: ConfigTab; label: string }[] = [
    { id: 'cycle', label: 'Loyalty Cycle' },
    { id: 'thresholds', label: 'Purchase Thresholds' },
    { id: 'general', label: 'General Settings' },
  ];

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Configuration</h1>

      {/* Tab Navigation */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex gap-4" aria-label="Configuration tabs">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`py-3 px-1 border-b-2 text-sm font-medium transition-colors ${
                activeTab === tab.id
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              }`}
              aria-selected={activeTab === tab.id}
              role="tab"
            >
              {tab.label}
            </button>
          ))}
        </nav>
      </div>

      {/* Tab Content */}
      <div role="tabpanel">
        {activeTab === 'cycle' && <CycleConfigSection />}
        {activeTab === 'thresholds' && <ThresholdsConfigSection />}
        {activeTab === 'general' && <GeneralConfigSection />}
      </div>
    </div>
  );
}
