// Ambient types shared across Blaizio.Base interop modules.
export {};

declare global {
  /** A Blazor <c>DotNetObjectReference</c> handed to JS for callbacks into .NET. */
  interface DotNetReference {
    invokeMethodAsync<T = void>(methodName: string, ...args: unknown[]): Promise<T>;
  }
}
