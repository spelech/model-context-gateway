export interface ParsedMcpName {
  serverId: string;
  cleanName: string;
  isCustom: boolean;
}

export function parseNamespacedName(name: string, defaultServer = 'custom'): ParsedMcpName {
  if (!name) return { serverId: defaultServer, cleanName: '', isCustom: true };

  const slashIndex = name.indexOf('/');
  if (slashIndex > 0) {
    return {
      serverId: name.substring(0, slashIndex),
      cleanName: name.substring(slashIndex + 1),
      isCustom: false,
    };
  }

  const dunderIndex = name.indexOf('__');
  if (dunderIndex > 0) {
    return {
      serverId: name.substring(0, dunderIndex),
      cleanName: name.substring(dunderIndex + 2),
      isCustom: false,
    };
  }

  const colonIndex = name.indexOf(':');
  if (colonIndex > 0) {
    return {
      serverId: name.substring(0, colonIndex),
      cleanName: name.substring(colonIndex + 1),
      isCustom: false,
    };
  }

  return { serverId: defaultServer, cleanName: name, isCustom: true };
}
