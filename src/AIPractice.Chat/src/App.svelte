<script lang="ts">
	let messagePromise: Promise<string> = loadMessage();

	async function loadMessage(): Promise<string> {
		const res = await fetch('/api/');
		if (!res.ok) {
			throw new Error('Failed to fetch');
		}
		return await res.text();
	}
</script>
<main>
    {#await messagePromise}
        <h1>Loading...</h1>
    {:then message}
        <h1>{message}</h1>
    {:catch error}
        <h1>{error}</h1>
    {/await}
</main>
