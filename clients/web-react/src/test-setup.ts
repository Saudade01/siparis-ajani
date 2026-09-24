import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Vitest "globals" kapalı olduğu için Testing Library kendi temizliğini kuramıyor.
afterEach(cleanup);
