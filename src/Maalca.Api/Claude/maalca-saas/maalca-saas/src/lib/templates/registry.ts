// src/lib/templates/registry.ts
// Maps business_type → public-page template component.
// To add a new vertical: create the template, register it here, add to enum in DB CHECK.

import type { ComponentType } from 'react';
import { RestaurantTemplate } from '@/components/public/templates/Restaurant';
import { BarberTemplate } from '@/components/public/templates/Barber';
import { ServiceTemplate } from '@/components/public/templates/Service';
import { RetailTemplate } from '@/components/public/templates/Retail';

export type BusinessType = 'restaurant' | 'barber' | 'service' | 'retail';

export interface PublicTemplateProps {
  business: {
    id: string;
    slug: string;
    name: string;
    description?: string | null;
    logo_url?: string | null;
    primary_color?: string | null;
    whatsapp?: string | null;
    business_type: BusinessType;
  };
  products: Array<{
    id: string;
    name: string;
    description?: string | null;
    price?: number | null;
    category?: string | null;
    image_url?: string | null;
    duration_min?: number | null;
  }>;
}

export const TEMPLATES: Record<BusinessType, ComponentType<PublicTemplateProps>> = {
  restaurant: RestaurantTemplate,
  barber: BarberTemplate,
  service: ServiceTemplate,
  retail: RetailTemplate,
};

export const BUSINESS_TYPE_LABELS: Record<BusinessType, string> = {
  restaurant: 'Restaurante',
  barber: 'Barbería',
  service: 'Servicios',
  retail: 'Tienda',
};
