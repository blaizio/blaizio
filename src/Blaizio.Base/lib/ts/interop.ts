/**
 * Guarded .NET callbacks. A JS-side event (animationend, a scroll report, a dismiss press) can
 * fire while - or just after - the owning component disposes its DotNetObjectReference: the
 * invocation then rejects with "There is no tracked object with id ..." and, because callback
 * sites are fire-and-forget, surfaces as an uncaught promise rejection in the console. That
 * teardown race is benign (the component is gone; there is nobody left to notify), so these
 * wrappers swallow exactly that rejection and keep every other failure loud.
 */

interface DotNetCallbackRef {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

/** The dispatcher's disposed-reference rejection (raced teardown), as opposed to a real failure. */
const isDisposedRefError = (e: unknown): boolean => {
  const message = e instanceof Error ? e.message : String(e);
  return message.includes('no tracked object') || message.includes('already disposed');
};

/**
 * Fire-and-forget callback into .NET. Null/undefined refs no-op; a disposed-reference rejection
 * is swallowed; anything else rethrows (stays an unhandled rejection, exactly as loud as before).
 */
export const invokeDotNet = (
  ref: DotNetCallbackRef | null | undefined,
  method: string,
  ...args: unknown[]
): Promise<unknown> =>
  ref
    ? ref.invokeMethodAsync(method, ...args).catch((e: unknown) => {
        if (!isDisposedRefError(e)) throw e;
      })
    : Promise.resolve();

/**
 * Result-bearing callback into .NET. A disposed-reference rejection resolves to
 * <paramref>fallback</paramref> instead; anything else rethrows.
 */
export const invokeDotNetResult = async <T>(
  ref: DotNetCallbackRef,
  fallback: T,
  method: string,
  ...args: unknown[]
): Promise<T> => {
  try {
    return (await ref.invokeMethodAsync(method, ...args)) as T;
  } catch (e) {
    if (isDisposedRefError(e)) return fallback;
    throw e;
  }
};
