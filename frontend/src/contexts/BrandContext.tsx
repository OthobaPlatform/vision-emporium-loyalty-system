import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';

export interface BrandConfig {
  companyName: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  logoUrl: string;
  faviconUrl: string;
}

const DEFAULT_BRAND: BrandConfig = {
  companyName: 'Vision Emporium',
  primaryColor: '#E31E24',
  secondaryColor: '#1a1a1a',
  accentColor: '#D6E4F0',
  logoUrl: '',
  faviconUrl: '',
};

interface BrandContextType {
  brandConfig: BrandConfig;
  isLoading: boolean;
  refreshBrand: () => Promise<void>;
}

const BrandContext = createContext<BrandContextType | undefined>(undefined);

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/v1';

function applyCssVariables(config: BrandConfig) {
  const root = document.documentElement;
  root.style.setProperty('--brand-primary', config.primaryColor);
  root.style.setProperty('--brand-secondary', config.secondaryColor);
  root.style.setProperty('--brand-accent', config.accentColor);
}

export function BrandProvider({ children }: { children: ReactNode }) {
  const [brandConfig, setBrandConfig] = useState<BrandConfig>(DEFAULT_BRAND);
  const [isLoading, setIsLoading] = useState(true);

  const fetchBrand = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/config/brand`);
      if (response.ok) {
        const data = await response.json();
        const config: BrandConfig = {
          companyName: data.companyName || DEFAULT_BRAND.companyName,
          primaryColor: data.primaryColor || DEFAULT_BRAND.primaryColor,
          secondaryColor: data.secondaryColor || DEFAULT_BRAND.secondaryColor,
          accentColor: data.accentColor || DEFAULT_BRAND.accentColor,
          logoUrl: data.logoUrl || DEFAULT_BRAND.logoUrl,
          faviconUrl: data.faviconUrl || DEFAULT_BRAND.faviconUrl,
        };
        setBrandConfig(config);
        applyCssVariables(config);
      } else {
        applyCssVariables(DEFAULT_BRAND);
      }
    } catch {
      // Silently fall back to defaults if API is unreachable
      applyCssVariables(DEFAULT_BRAND);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchBrand();
  }, []);

  const refreshBrand = async () => {
    await fetchBrand();
  };

  return (
    <BrandContext.Provider value={{ brandConfig, isLoading, refreshBrand }}>
      {children}
    </BrandContext.Provider>
  );
}

export function useBrand(): BrandContextType {
  const context = useContext(BrandContext);
  if (context === undefined) {
    throw new Error('useBrand must be used within a BrandProvider');
  }
  return context;
}
